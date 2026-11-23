# TOH-Shell 共通引き継ぎ

更新日 2026-11-24（JST）

## 最初に確認すること

**Windows版の共通開発元は `C:/Users/Owner/.codex/visualizations/2026/09/18/01a0b3fc-0be8-7c71-b28d-f0568f4338bf/mad-alternate-20261123-133208-111/repo` です。この `docs/AI_HANDOFF.md` が正本です。**

開発ツールでこの既存フォルダーを開き、[AGENTS.md](../AGENTS.md) と今回の依頼を読んでください。各ツールが以前開いていたDownloadsの旧本体や公開用コピーを選んでいないことを、次の読み取りコマンドで確認してください。

```sh
git rev-parse --show-toplevel
git status --short --branch
git log -1 --oneline
git diff --stat
git diff --cached --stat
```

別コピーで固有の変更を調査する場合も、このメモを参照し、実際に調査した場所を結果に書いてください。進み具合が記録と違う場合は、変更前に差分を読み、既存の変更を保全してください。

## 現行開発元のGit状態

- ブランチ `codex/mad-alternate`。
- v2.2.4 の公開コミットは `20d1fd9f398b9d62b35692fa594dd7892d3d0975`（`Release: TOH-Shell v2.2.4 [skip ci]`）で、ローカルの公開タグも同コミットを指す。
- 2026-11-24、ユーザーの「Gitして」指示で下表の未公開変更30ファイルをすべてコミット `cc2abd2`（マッドアルターネイト追加、バグ報告フォーム、配信コメント枠のタスク中固定ほか）。同日「pushして」の指示で GitHub に新規ブランチ `codex/mad-alternate` として push し、追跡先を `origin/codex/mad-alternate` に設定（リモートとローカルが `cc2abd2` で一致を確認）。GitHub の `main` は v2.2.4 の `20d1fd9` のまま変更していない。HEADは `cc2abd2`、その親が v2.2.4 の `20d1fd9`。
- 同日「Gitとプッシュどっちもして」の指示で、タイガー捕食死体の会議表示修正（`Patches/PlayerContorols/ReportDeadBodyPatch.cs`）とこの文書を `cc2abd2` の次のコミットとして記録し、`origin/codex/mad-alternate` へ push。コミットIDは `git log -1` で確認する。
- 公開時のGitHub `main` とタグを参照する保存用コピーは別にあり、今回も作業ツリーはクリーン。今回リモートは取得していない。

### 未公開の変更

| 対象 | 完了事項とファイル |
| --- | --- |
| マッドアルターネイト | 全タスク完了後の自投票で保護相手を選択。会議終了後から次の会議までのキル判定で身代わりになり、死因は「殉死」。エンジニアベース、ベント初期値OFF。未追跡の `Roles/Madmate/MadAlternate.cs` と、追跡済みの `Roles/Core/CustomRoleManager.cs`、`Modules/ExtendedPlayerControl/ExtendedRpc.cs`、`Resources/string.csv`、`main.cs` |
| タスク設定 | 通常・ショート・ロングともに0〜10、1刻み、初期値3。既存の11以上は10として扱う。変更 `Modules/OptionRoles.cs`、`Roles/Madmate/MadAlternate.cs` |
| タイガー | 捕食開始時のキルクール解除をルーラー・チューナーの補正後に適用。通常キルへの補正と時間切れ時の自滅は従来の処理を使用。変更 `Roles/Neutral/Tiger.cs`、`Modules/GameOptionsSender/PlayerGameOptionsSender.cs` |
| ロビーのバグ報告フォーム | `Patches/RoleGuideButtonPatch.cs` と `Patches/BugReportPanel.cs`。BUGボタンから件名・本文・任意のログ添付があるゲーム内フォームを開く。試合・練習・設定画面では非表示。受付未設定時は送信を無効にする。Steam上の前版で、ボタン表示とDiscordリンクが動作したことはユーザー確認済み。 |
| 報告フォームの文字入力 | ユーザーが入力文字の消失を報告。 `Patches/BugReportPanel.cs` と `Modules/BugReportInputText.cs` で、IME、確定文字、カーソルの描画を分離。入力中の欄は青枠で表示し、点滅するカーソルの位置に合わせて長文の表示ページを切り替える。HUDテスト217件成功（入力関連を4件追加）。 |
| 報告フォームのIME確定 | 入力表示修正版で「テスト用」の「テスト」がEnterや欄切り替えで消えるとユーザー報告。フォーム独自の下書きで確定済み文字と変換中の文字を保持し、ネイティブの確定結果を優先しつつ、確定通知が欠けた場合に画面上の変換文字を保存する。Tabまたはクリックで欄を切り替える。PC回帰テストにこの操作、IME結果の二重反映防止、取消を追加。 |
| 報告送信・診断ログ | `Modules/BugReportSubmission.cs` からHTTPS中継へ明示的に送信。`Modules/BugReportRoundLog.cs` と `Modules/Debugger.cs` は直近の1試合の状態・警告の箇所を最大160行保持。添付は初期状態でオフ。ゲームコード・全文ログ・認証情報はリクエストに含めない。 |
| Discordフォーラム中継 | `tools/bug-report-relay/` にWorker、運用手順、オフラインテストを追加。認可・レート制限の後、固定のフォーラムを照合し、既存Shell バグ修正Bot名義で新規スレッドを作る設計。Botの設定と自動修正のコードは変更していない。 |
| タイガー捕食死体の会議表示（`cc2abd2` の次のコミット） | 捕食された死体が会議画面に出るとユーザー報告。インセンダー等の自動通報 `ExReportDeadBody`（Cancelcheck=false で `CheckMeeting` を通らない）とその先の `WaitMeeting` に捕食死体の除外がなく、マッドクリーナーと同じ位置に `Tiger.IsPredatedBody` の除外を追加。捕食死体への自動通報では会議自体が始まらない（既存の通常通報の扱いと同じ）。変更 `Patches/PlayerContorols/ReportDeadBodyPatch.cs` |
| 配信コメント枠のドラッグ固定 | タスクなどのミニゲーム画面（`Minigame.Instance`）が開いている間は、コメント枠をつかんで動かせない。ドラッグ中にタスク画面が開いた場合は、その位置で止めて保存する。変更 `Modules/Streamer/StreamCommentFrame.cs` |
| 検証コード | 未追跡の `tests/MadAlternate.Runtime.Tests/` 内のプロジェクト・`Program.cs`・`Stubs.cs`、計3ファイル |
| HUD回帰テスト | `tests/GameplayHudUi.Tests/` にバグ報告の回帰テストを追加。製品コードの抽出・由来情報、ログとHTTPクライアントの参照、スタブを更新。 |
| 引き継ぎ文書 | 追跡済みで変更ありの `AGENTS.md` と、未追跡の `docs/AI_HANDOFF.md` |

## 完了済みの検証と配布

| 項目 | 確認内容 |
| --- | --- |
| Windowsの最終Releaseビルド | エラー0件、既存警告19件 |
| 戦闘・タスク関連の自動テスト | 最終DLLの作成時にタイガーのPC模擬テスト11件、マッドアルターネイトのPCテスト33件成功。タスク上限の変更時に設定IDの回帰テスト41件成功 |
| Steam配置済みDLL | 現在はタイガー捕食死体の会議表示修正版（下の「タイガー捕食死体の会議表示の検証」参照、コメント枠固定も含む）。以下はその前の記録: IME確定修正版を `C:/Program Files (x86)/Steam/steamapps/common/Among Us/BepInEx/plugins/TownOfHost-Shell.dll` に配置。現行SHA-256はビルド品と同じ `DAB3644E819CE296AD615AEAEDE4CF698910FF1AC9875309D5480F9C492D7828`。旧DLLと一致する `bug-report-input-20261124/release/TownOfHost-Shell.dll` を復元用に保持（SHA-256 `0EEB620A913770DEA704E3470BA703D937CF8A78A0C4B9FB0F61BCA3D501441F`）。報告用設定ファイルのハッシュは配置前後で一致。 |
| 公開済みv2.2.4 | 正式版を公開済み。Steam版・Epic版のZIPとDLL単体を配布。公開DLLのSHA-256は `CC5020B79BC9BBE689E15743966271B5AC99CADAAEB8A6F42BBECF228FDAAAE9` |
| 引き継ぎ環境 | Gitとソース差分を読み取り、正本を現行開発元に設定。別ツールでの読み取りと `git status --short --branch` の実行をユーザー報告で確認。 |
| ロビーHUDのPCテスト | 209件成功。新規5件で表示条件、クリック時の遷移、ヘッダー余白を確認。 |
| ロビーHUD変更後のReleaseビルド | エラー0件、既存警告19件。リポジトリの親にある `lobby-bug-report-20261124/` にDLLとビルド・テストログを保存。 |
| ゲーム内フォーム版の検証 | Release成功、エラー0件・既存警告19件。PCのHUD・送信・ログテスト213件成功、Discordを模擬した中継テスト7件成功。親フォルダーの `bug-report-form-20261124/` にビルド・テストログとDLLを保存。ゲーム内操作は未確認。 |
| 入力表示修正版の検証 | Release成功、エラー0件・既存警告19件。ビルドログとDLLは親の `bug-report-input-20261124/`。Steam配置済み。実ゲームでのカーソルの位置、長文の表示、入力後の再送信は未確認。 |
| IME確定修正版の検証 | Release成功、エラー0件・既存警告19件。PC HUDテスト222件成功。ビルドとテストのログおよびDLLは親の `bug-report-ime-20261124/`。Steam配置済み。Windows IMEの実ゲームでの再確認は未実施。 |
| タイガー捕食死体の会議表示の検証 | Releaseビルド成功（`-p:CopyToAmongUs=false`、一時フォルダー出力）、エラー0件・既存警告19件。`tests/Camouflage.EndGame.Tests` 54件、`tests/Hamo44002.Completion.Tests` 169件、`tests/Hamo44004.Runtime.Tests` 138件、親の `tiger-predation-20261123-165246-303/regression` 11件すべて成功。その後、親の `tiger-meeting-body-20261124/` に再ビルド（エラー0件・既存警告19件、ログ `build.log`）し、Steamの `BepInEx/plugins/TownOfHost-Shell.dll` へ配置。配置後のSHA-256はビルド品と同じ `13C77B63C7DB19707E9C21BA60C2E05BA03442B64E9E2B533577C2F4D65671ED`。配置前のコメント枠固定版（`771F010D…2E4E`）は同フォルダーの `backup/` に保存。実ゲームでインセンダー等を捕食した時の確認は未実施。 |
| コメント枠ドラッグ固定の検証 | Releaseビルド成功（`-p:CopyToAmongUs=false`、出力は一時フォルダー）、エラー0件・既存警告19件。配信テスト `tests/Streaming43505.Tests` 341件成功。その後、親フォルダーの `comment-drag-lock-20261124/` に再ビルド（エラー0件・既存警告19件、ログ `build.log`）し、Steamの `BepInEx/plugins/TownOfHost-Shell.dll` へ配置。配置後のSHA-256はビルド品と同じ `771F010DB76A0D426F25CED641F40135115D44CACA4136126E3BD5CFD7EC2E4E`。配置前のIME確定修正版（`DAB3644E…7828`）は同フォルダーの `backup/` に保存。実ゲームでタスク中に枠が動かないことは未確認。 |
| DiscordとCloudflareの実運用確認 | 許可されたアカウントに公式Workers APIで専用Worker `toh-shell-bug-report-relay` を作成し、既存BotとSteam版のフォームを接続。受付キーなしの匿名通信はHTTP 401、受付キー付きの読取診断はHTTP 200。初回はWorkersが対応しない `redirect: "error"` によりDiscordへの送信前に停止。投稿が0件であることを読み取り確認後、`manual` に修正。中継テスト8件成功。実フォーラムで投稿は1件のみ。Bot名義、本文、「その他」タグ、架空ログ添付を確認済み。[テスト投稿](https://discord.com/channels/1543630978218721412/1574454979136065556) |

GitHubの配布物とリリースノートは [正式リリース v2.2.4](https://github.com/sijimi2222/TownOfHost-Shell/releases/tag/v2.2.4)。

過去のビルド・テスト・配置記録は、このリポジトリの親フォルダーにある `tiger-predation-20261123-165246-303/` と `task-cap-20261123-162823-093/`。どちらも `deployment.json` に対応するDLLと配置前バックアップの所在が記録されている。

## 既存コピーの比較

Git履歴はローカルオブジェクトで比較し、差分の重複は実ファイルの内容でも確認した。表の件数は「未ステージ / ステージ済み / 無視対象を除く未追跡ファイル」。履歴だけでなく固有の変更も保持するため、今回ソースの統合は行っていない。

### 主なコピー

| コピーの絶対パス | ブランチ・HEAD・最新コミット | 差分件数 | v2.2.4との関係・固有の内容・用途 |
| --- | --- | --- | --- |
| `C:/Users/Owner/.codex/visualizations/2026/09/18/01a0b3fc-0be8-7c71-b28d-f0568f4338bf/mad-alternate-20261123-133208-111/repo` | `codex/mad-alternate` / `20d1fd9` / Release: TOH-Shell v2.2.4 | 15 / 0 / 14 | 公開コミットと同じHEAD。最新の未公開役職・HUD・報告差分を保持する共通開発元 |
| `C:/Users/Owner/.codex/visualizations/2026/09/18/01a0b3fc-0be8-7c71-b28d-f0568f4338bf/release-v224-20261123-092543-437/main` | `main` / `20d1fd9` / Release: TOH-Shell v2.2.4 | 0 / 0 / 0 | 公開時のビルド元とタグを確認できる保存用 |
| `C:/Users/Owner/Downloads/TownOfHost-hamo-4.00.00.21 (1)/TownOfHost-hamo-4.00.00.21` | `main` / `9632e34` / あいうえお順、From順ボタン追加前 | 1 / 0 / 2 | v2.1.0系。v2.2.4側に9、旧本体側に1コミットの分岐。旧本体側のコミットには二つ名・報告関連など、現行コピーにないコードがある。未追跡の添付ファイルを保全 |
| `C:/Users/Owner/.codex/visualizations/2026/09/18/01a0b3fc-0be8-7c71-b28d-f0568f4338bf/nova-controller-git-20261108-095247-705/repo` | `codex/nova-debug-controller-v2.2.3` / `a320745` / fix: stabilize HUD header layout and improve Nova Airship range | 51 / 0 / 86 | 両側に2固有コミット。v2.2.4は内容を取り込んだ別コミット。作業差分137パス中、公開時点と130が一致、2はテスター用バージョン差、5はローカル運用・診断メモとして旧コピーに残る |
| `C:/Users/Owner/.codex/visualizations/2026/09/18/01a0b3fc-0be8-7c71-b28d-f0568f4338bf/release-v223-20261106-175733/main` | `main` / `00da718` / docs: record administrator platform smoke tests | 0 / 0 / 0 | v2.2.4の祖先。v2.2.3の公開・検証記録用 |
| `C:/Users/Owner/.codex/visualizations/2026/09/18/01a0b3fc-0be8-7c71-b28d-f0568f4338bf/legacy-hamo-metadata-20261105-222619/main` | `main` / `53bd66d` / docs: identify legacy TOH-hamo as Shell base | 0 / 0 / 0 | v2.2.4の祖先。出典表記の履歴用 |
| `C:/Users/Owner/.codex/visualizations/2026/09/18/01a0b3fc-0be8-7c71-b28d-f0568f4338bf/main-upstream-docs-20261105-214757/main` | `main` / `a39c5f9` / docs: clarify TOH-K update tracking | 0 / 0 / 0 | v2.2.4の祖先。上流追跡文書の履歴用 |

旧本体のローカル追跡参照 `origin/main` は `3ccb452`（旧本体が1コミット先）。未追跡ファイルは引き継ぎメモと以前からある添付ファイルで、添付の内容はこの文書に記録しない。

### 追加の履歴・保管コピー

| コピーの絶対パス | ブランチ・HEAD・最新コミット | 差分件数 | 関係・保全事項 |
| --- | --- | --- | --- |
| `C:/Users/Owner/Downloads/TownOfHost-hamo-4.00.00.21` | `main` / `8bb7be6` / Update GitHub button URL | 10 / 0 / 0 | v2.2.4の祖先。追放・会議・公開通知などに固有のローカル差分 |
| `C:/Users/Owner/Downloads/TOH-Shell-Local-Git/shell-development-backup-20261018` | `codex/shell-development-backup` / `96e8a4e` / docs: record Git line-ending integrity for local snapshot | 0 / 0 / 0 | 共通祖先のない保存履歴。旧版のソースとサービス用コードの保管先 |
| `C:/Users/Owner/Downloads/TOH-Shell-Local-Git/matchmaking-restoration-success-20261019` | `codex/matchmaking-restoration-success` / `b9ed0e4` / fix: restore TOH-Shell matchmaking and Discord relay | 0 / 0 / 0 | 共通祖先のない保存履歴。マッチメイキング・Discordリレー復元の実装と記録 |
| `C:/Users/Owner/Downloads/TOH-Shell-Workspaces/20261017-Phase6A3-Protected-080602/release-development/publication-v2.2.0-beta.1/source` | `backup/pre-hamo-v4.35.01` / `cb5e400` / Save TOH-Shell v4.35.00 integration before hamo v4.35.01 update | 0 / 883 / 882 | 共通祖先のない旧ベータ準備。既存のステージと未追跡状態を保全 |
| `C:/Users/Owner/Downloads/TOH-Shell-Workspaces/20261017-Phase6A3-Protected-080602/release-development/publication-v2.2.0-beta.1-final/source` | `codex/release-v2.2.0-beta.1` / `0dc3dfc` / Prepare TOH-Shell v2.2.0 beta | 0 / 0 / 0 | 共通祖先のない旧ベータ履歴。次行のリンク先リポジトリ。旧ベータ版の記録用 |
| `C:/Users/Owner/Downloads/TOH-Shell-Workspaces/20261017-Phase6A3-Protected-080602/release-development/hamo43505-development/source` | `codex/integrate-hamo-v4.35.05` / `1756344` / docs: clarify TOH-K update tracking | 68 / 0 / 31 | 共通祖先のない旧移植履歴。現行との内容比較では99変更パス中86が一致し、13に差が残る。Nova・ゲーム開始・死体通報などの差分を保全 |
| `C:/Users/Owner/Downloads/TOH-Shell-Workspaces/20261017-Phase6A3-Protected-080602/release-development/git-backup-pre-hamo-v4.35.01-20261017-170954` | `backup/pre-hamo-v4.35.01` / `cb5e400` / Save TOH-Shell v4.35.00 integration before hamo v4.35.01 update | 0 / 0 / 0 | 共通祖先のない旧統合バックアップ。旧実装の比較用 |
| `C:/Users/Owner/AppData/Local/Temp/TownOfHost-Shell-discord-notify-20260920` | detached / `a0d004d` / Add Discord GitHub notification（旧本体のworktree登録記録） | 確認不可 | リンク先が存在しないため、旧本体の登録情報のみ。登録はそのまま保全 |

**選定理由**。現行開発元は公開済みv2.2.4の正確なコミットを起点とし、今回必要な未公開修正をすでに持つ。公開用コピーも既存のまま独立して保管でき、作業を始めるためのコピー作成、履歴操作、ソース移動が不要。旧版固有の内容には依存があるため、必要になった作業で個別に比較する。

## 未確認事項と次の作業

1. **引き継ぎの読み取り確認**。新しく利用するツールでは、この共通開発元のパス・ブランチ・HEADと未公開差分を読み取り専用で確認する。
2. **Windowsの実ゲーム確認**。次回は非公開で、ルーラー・チューナーの延長中にタイガーが捕食直後にキルできるかを確認する。マッドアルターネイトは3種のタスク上限10、身代わりと「殉死」、次の会議での失効を確認する。
3. **旧コピー固有の機能**。二つ名、報告、リレー、旧移植の差分を一括で移す作業は未実施。必要な機能だけ、依頼された範囲で履歴と差分を照合する。
4. **Android**。別プロジェクト `C:/Users/Owner/.codex/visualizations/2026/09/18/01a0b3fc-0be8-7c71-b28d-f0568f4338bf/toh-shell-android` に保管。Starlight優先でStaff承認待ち、Androidホストは全員OFF。次工程はAndroid側の `docs/STARLIGHT-BODY-PORT-ja.md` と `docs/STARLIGHT-PARTICIPANT-DRAFT-ja.md` を参照する。
5. **ゲーム内のバグ報告**。Steam版フォームDLLとこのPC用の受付設定は準備済み。許可された既存フォーラムへの動作テスト1件は、フォームと同じ中継APIを使う検証ツールから送信し、実投稿を確認済み。投稿記録は親の `bug-report-form-20261124/cloudflare-deploy/discord-test-result.json`。テストの送信許可は消費済みで再送しない。実ゲーム画面での入力・クリックは確認していない。別PCに試験導入する過程では[中継の導入手順](../tools/bug-report-relay/README-ja.md)を参照。Nightly RepairはBot投稿を自動修正の対象から外す既存仕様を維持する。

## 次のAIに渡す短い文

> Windows版TOH-Shellの共通開発元として `C:/Users/Owner/.codex/visualizations/2026/09/18/01a0b3fc-0be8-7c71-b28d-f0568f4338bf/mad-alternate-20261123-133208-111/repo` を開いてください。まず `AGENTS.md` と `docs/AI_HANDOFF.md` を読み、読み取り専用でパス、ブランチ、HEAD、未公開の役職修正を確認して報告してください。その後の作業では既存差分を保全し、終了時に同じ引き継ぎメモを更新してください。
