const maxRequestBytes = 32 * 1024;
const snowflake = /^[0-9]{17,20}$/;
const accessKeyFormat = /^[A-Za-z0-9_-]{32,128}$/;
const diagnosticLine = /^\d+\.\d{3}s (?:round_started|meeting_started|round_ended|left_game|returned_to_lobby|(?:Warning|Error|Fatal) (?:[A-Za-z0-9._-]{1,60}):\d+)$/;

function reply(status, body) {
    return Response.json(body, { status, headers: { "Cache-Control": "no-store" } });
}

async function readReport(request) {
    const reader = request.body?.getReader();
    if (!reader) return null;
    const decoder = new TextDecoder("utf-8", { fatal: true });
    let json = "";
    let size = 0;
    try {
        while (true) {
            const { done, value } = await reader.read();
            if (done) break;
            size += value.byteLength;
            if (size > maxRequestBytes) {
                await reader.cancel();
                return null;
            }
            json += decoder.decode(value, { stream: true });
        }
        const report = JSON.parse(json + decoder.decode());
        if (!report || typeof report !== "object" || Array.isArray(report)) return null;
        if (Object.keys(report).some(key => !["title", "description", "version", "diagnostics"].includes(key))) return null;
        const { title, description, version, diagnostics } = report;
        if (typeof title !== "string" || !title.trim() || title.length > 80 || /[\u0000-\u001F\u007F]/u.test(title)) return null;
        if (typeof description !== "string" || !description.trim() || description.length > 1700
            || /[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F]/u.test(description)) return null;
        if (typeof version !== "string" || !/^[A-Za-z0-9._+-]{1,32}$/.test(version)) return null;
        if (diagnostics != null) {
            if (typeof diagnostics !== "string" || diagnostics.length > 16000) return null;
            const lines = diagnostics.split("\n");
            if (lines.length > 160 || !lines.every(line => diagnosticLine.test(line))) return null;
        }
        return { title: title.trim(), description: description.trim(), version, diagnostics };
    } catch {
        return null;
    } finally {
        reader.releaseLock();
    }
}

function authHeaderMatches(request, key) {
    const supplied = request.headers.get("Authorization") ?? "";
    const expected = `Bearer ${key}`;
    if (supplied.length !== expected.length) return false;
    let difference = 0;
    for (let i = 0; i < expected.length; i++)
        difference |= supplied.charCodeAt(i) ^ expected.charCodeAt(i);
    return difference === 0;
}

export async function handleReport(request, env, discordFetch = (url, options) => fetch(url, options)) {
    if (new URL(request.url).pathname !== "/v1/bug-reports") return reply(404, { error: "not_found" });
    const checkOnly = request.method === "GET";
    if (!checkOnly && request.method !== "POST") return reply(405, { error: "method_not_allowed" });
    const forumId = env.DISCORD_FORUM_ID;
    const guildId = env.DISCORD_GUILD_ID;
    const tagId = env.DISCORD_FORUM_TAG_ID || "";
    if (!snowflake.test(forumId ?? "") || !snowflake.test(guildId ?? "") || (tagId && !snowflake.test(tagId))
        || !env.DISCORD_BOT_TOKEN || !accessKeyFormat.test(env.REPORT_ACCESS_KEY ?? "")
        || !env.REPORTS_PER_IP || !env.REPORTS_GLOBAL)
        return reply(503, { error: "not_configured" });
    if (!authHeaderMatches(request, env.REPORT_ACCESS_KEY)) return reply(401, { error: "unauthorized" });
    if (!checkOnly && !/^application\/json(?:\s*;|$)/i.test(request.headers.get("Content-Type") ?? ""))
        return reply(415, { error: "unsupported_content_type" });
    if (Number(request.headers.get("Content-Length")) > maxRequestBytes)
        return reply(413, { error: "too_large" });

    const source = request.headers.get("CF-Connecting-IP") ?? "unknown";
    if (!(await env.REPORTS_PER_IP.limit({ key: source })).success
        || !(await env.REPORTS_GLOBAL.limit({ key: "forum" })).success)
        return reply(429, { error: "rate_limited" });
    const report = checkOnly ? null : await readReport(request);
    if (!checkOnly && !report) return reply(400, { error: "invalid_report" });

    const url = `https://discord.com/api/v10/channels/${forumId}`;
    const headers = {
        Authorization: `Bot ${env.DISCORD_BOT_TOKEN}`,
        "User-Agent": "DiscordBot (https://github.com/sijimi2222/TownOfHost-Shell, 2.2.4)"
    };
    let creatingThread = false;
    try {
        // Workers supports manual redirects. Keep Discord credentials on the fixed origin.
        const channel = await discordFetch(url, { headers, redirect: "manual", signal: AbortSignal.timeout(6000) });
        if (!channel.ok) return reply(channel.status === 429 ? 429 : 503,
            { error: "forum_unavailable", upstreamStatus: channel.status });
        const forum = await channel.json();
        if (forum.id !== forumId || forum.guild_id !== guildId || forum.type !== 15 || forum.name !== "バグ報告")
            return reply(503, { error: "forum_mismatch" });
        if (((forum.flags & 16) && !tagId) || (tagId && !forum.available_tags?.some(tag => tag.id === tagId)))
            return reply(503, { error: "forum_tag_mismatch" });
        if (checkOnly) return reply(200, { ready: true });

        const payload = {
            name: report.title,
            message: { content: `TOH-Shell v${report.version} · ゲーム内報告\n\n${report.description}`,
                allowed_mentions: { parse: [], replied_user: false } }
        };
        if (tagId) payload.applied_tags = [tagId];

        let body;
        if (report.diagnostics) {
            payload.message.attachments = [{ id: 0, filename: "toh-shell-round.log" }];
            body = new FormData();
            body.set("payload_json", JSON.stringify(payload));
            body.set("files[0]", new Blob([report.diagnostics], { type: "text/plain" }), "toh-shell-round.log");
        } else {
            headers["Content-Type"] = "application/json";
            body = JSON.stringify(payload);
        }
        creatingThread = true;
        const result = await discordFetch(`${url}/threads`, { method: "POST", headers, body, redirect: "manual",
            signal: AbortSignal.timeout(9000) });
        if (!result.ok) return reply(result.status === 429 ? 429 : 502,
            { error: "discord_rejected", upstreamStatus: result.status });
        const thread = await result.json();
        if (typeof thread.id !== "string" || !snowflake.test(thread.id) || thread.parent_id !== forumId)
            return reply(502, { error: "delivery_unconfirmed" });
        return reply(201, { threadId: thread.id });
    } catch {
        return creatingThread
            ? reply(502, { error: "delivery_unconfirmed" })
            : reply(503, { error: "forum_connection_failed" });
    }
}

export default { fetch: (request, env) => handleReport(request, env) };
