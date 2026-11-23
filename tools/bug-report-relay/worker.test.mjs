import assert from "node:assert/strict";
import { test } from "node:test";
import { handleReport } from "./worker.mjs";

const forumId = "101010101010101010";
const guildId = "202020202020202020";
const threadId = "303030303030303030";
const tagId = "404040404040404040";
const key = "a".repeat(32);
const validReport = { title: "ロビーで停止", description: "本文 @everyone", version: "2.2.4", diagnostics: null };
const log = "0.000s round_started\n0.020s Error Test.cs:42\n15.100s round_ended";

function fixture({ forum = {}, allow = true, postStatus = 201, replyParent = forumId, tag = "" } = {}) {
    const calls = [];
    const configuredEnv = {
        DISCORD_FORUM_ID: forumId, DISCORD_GUILD_ID: guildId, DISCORD_FORUM_TAG_ID: tag,
        DISCORD_BOT_TOKEN: "test-bot-placeholder", REPORT_ACCESS_KEY: key,
        REPORTS_PER_IP: { limit: async () => ({ success: allow }) },
        REPORTS_GLOBAL: { limit: async () => ({ success: true }) }
    };
    const route = async (report = validReport, { auth = key, raw = false, method = "POST" } = {}) => {
        const request = new Request("https://example.invalid/v1/bug-reports", {
            method,
            headers: { Authorization: `Bearer ${auth}`, "Content-Type": "application/json" },
            body: method === "GET" ? undefined : raw ? report : JSON.stringify(report)
        });
        return handleReport(request, configuredEnv, async (url, options) => {
            calls.push({ url, ...options });
            if (!options.method)
                return Response.json({ id: forumId, guild_id: guildId, type: 15, name: "バグ報告", ...forum });
            return Response.json({ id: threadId, parent_id: replyParent }, { status: postStatus });
        });
    };
    return { calls, route, configuredEnv };
}

test("creates a single Discord forum thread, with mentions disabled and no attachment by default", async () => {
    const { calls, route } = fixture();
    const result = await route();
    assert.equal(result.status, 201);
    assert.deepEqual(await result.json(), { threadId });
    assert.equal(calls.length, 2);
    assert.ok(calls.every(call => call.redirect === "manual"));
    assert.equal(calls[1].url, `https://discord.com/api/v10/channels/${forumId}/threads`);
    assert.equal(calls[1].headers.Authorization, "Bot test-bot-placeholder");
    const payload = JSON.parse(calls[1].body);
    assert.equal(payload.name, validReport.title);
    assert.match(payload.message.content, /本文 @everyone/);
    assert.deepEqual(payload.message.allowed_mentions.parse, []);
    assert.equal(payload.message.attachments, undefined);
});

test("optional round log is a multipart attachment in the first post", async () => {
    const { calls, route } = fixture();
    assert.equal((await route({ ...validReport, diagnostics: log })).status, 201);
    const form = calls[1].body;
    assert.equal(form.get("files[0]").name, "toh-shell-round.log");
    assert.equal(await form.get("files[0]").text(), log);
    assert.deepEqual(JSON.parse(form.get("payload_json")).message.attachments,
        [{ id: 0, filename: "toh-shell-round.log" }]);
});

test("missing and incorrect access keys stop before Discord is contacted", async () => {
    const { calls, route, configuredEnv } = fixture();
    assert.equal((await route(validReport, { auth: "wrong" })).status, 401);
    delete configuredEnv.REPORT_ACCESS_KEY;
    assert.equal((await route()).status, 503);
    assert.equal(calls.length, 0);
});

test("authenticated readiness checks use DiscordBot identity without creating a post", async () => {
    const { calls, route } = fixture();
    const result = await route(validReport, { method: "GET" });
    assert.equal(result.status, 200);
    assert.deepEqual(await result.json(), { ready: true });
    assert.equal(calls.length, 1);
    assert.match(calls[0].headers["User-Agent"], /^DiscordBot \(/);
    assert.equal(await route(validReport, { method: "GET", auth: "" }).then(response => response.status), 401);
    assert.equal(calls.length, 1);
});

test("rate limits stop submissions before Discord is contacted", async () => {
    const { calls, route } = fixture({ allow: false });
    assert.equal((await route()).status, 429);
    assert.equal(calls.length, 0);
});

test("validates lengths, private log shape, and blocks request-controlled destinations", async () => {
    const { calls, route } = fixture();
    for (const report of [
        { ...validReport, title: " " },
        { ...validReport, description: "x".repeat(1701) },
        { ...validReport, diagnostics: "[session] arbitrary raw log" },
        { ...validReport, forumId: "any" }
    ]) {
        assert.equal((await route(report)).status, 400);
    }
    assert.equal((await route("x".repeat(40_000), { raw: true })).status, 400);
    assert.equal(calls.length, 0);
});

test("checks Discord forum kind, server, name and mandatory tag before writing", async () => {
    for (const forum of [{ type: 0 }, { guild_id: "incorrect" }, { name: "役職提案" }, { flags: 16 }]) {
        const { calls, route } = fixture({ forum });
        assert.equal((await route()).status, 503);
        assert.equal(calls.length, 1);
    }
    const { calls, route } = fixture({ forum: { flags: 16, available_tags: [{ id: tagId }] }, tag: tagId });
    assert.equal((await route()).status, 201);
    assert.deepEqual(JSON.parse(calls[1].body).applied_tags, [tagId]);
});

test("reports Discord rate limits and ambiguous delivery without a duplicate post", async () => {
    for (const [opts, expected] of [
        [{ postStatus: 429 }, 429], [{ postStatus: 500 }, 502], [{ postStatus: 307 }, 502],
        [{ replyParent: "incorrect" }, 502]
    ]) {
        const { calls, route } = fixture(opts);
        assert.equal((await route()).status, expected);
        assert.equal(calls.length, 2);
    }
});
