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

- v2.2.5 のリリース対象は、v2.2.4以降のWindows版の役職・フォーム・軽量化の変更。バージョン表記を更新し、公開用の変更点を [v2.2.5 リリースノート](releases/v2.2.5.md) に記載。以下の版や未公開という表記は、各作業時点の記録。

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
| 報告フォームのログ添付表示（ビルド済み・Steam配置済み） | `Patches/BugReportPanel.cs`。添付オン・オフを文字と色で表示し、ログがない時は専用表示にする。送信中は切り替えを止め、投稿完了後はその送信での添付あり・なしを表示する。画面の数値は中継が返すDiscordスレッドIDだったため完了画面から省いた。HUDテスト222件、中継テスト8件成功。Release成功（エラー0、既存のC#警告19件と改行に関するGit警告2件）。ビルド品は `../bug-report-attachment-ui-20261124/release/TownOfHost-Shell.dll`、SHA-256 は `01C79D6830EBEF406274C432C52A7A557F1B0DCE0D445782870AD7324FC82ABB`。ユーザー指示でSteamに配置し、ビルド品とのSHA-256一致を確認。ゲーム画面での確認・実投稿は未実施。 |
| 報告送信・診断ログ | `Modules/BugReportSubmission.cs` からHTTPS中継へ明示的に送信。`Modules/BugReportRoundLog.cs` と `Modules/Debugger.cs` は直近の1試合の状態・警告の箇所を最大160行保持。添付は初期状態でオフ。ゲームコード・全文ログ・認証情報はリクエストに含めない。 |
| Discordフォーラム中継 | `tools/bug-report-relay/` にWorker、運用手順、オフラインテストを追加。認可・レート制限の後、固定のフォーラムを照合し、既存Shell バグ修正Bot名義で新規スレッドを作る設計。Botの設定と自動修正のコードは変更していない。 |
| タイガー捕食死体の会議表示（`cc2abd2` の次のコミット） | 捕食された死体が会議画面に出るとユーザー報告。インセンダー等の自動通報 `ExReportDeadBody`（Cancelcheck=false で `CheckMeeting` を通らない）とその先の `WaitMeeting` に捕食死体の除外がなく、マッドクリーナーと同じ位置に `Tiger.IsPredatedBody` の除外を追加。捕食死体への自動通報では会議自体が始まらない（既存の通常通報の扱いと同じ）。変更 `Patches/PlayerContorols/ReportDeadBodyPatch.cs` |
| 配信コメント枠のドラッグ固定 | タスクなどのミニゲーム画面（`Minigame.Instance`）が開いている間は、コメント枠をつかんで動かせない。ドラッグ中にタスク画面が開いた場合は、その位置で止めて保存する。変更 `Modules/Streamer/StreamCommentFrame.cs` |
| ノヴァの軽量化（未コミット・未ビルド） | 「ノヴァ配役時にホストがクラッシュする」とのユーザー報告。ログはなく、原因は特定していない。依頼どおり、仕様・長さを変えずに毎フレームの処理を軽くした。`Roles/Impostor/Nova.cs` で、毎フレーム呼ばれる `TilesReady` / `RenderingCommitted` / `VisualStatus` と、ダミーごとの「まだ使えるか」判定（`tiles.Any(t => t.Failed)` をダミー数×毎フレーム実行していた）を、LINQ・ラムダからループに置き換えた。判定関数はダミーごとに作らず1つを共有し（`NewTile()`）、`VisualStatus` は1フレームに1回だけ計算する。フォントの寸法計算は `UseSmoothFont` に切り出しただけで、探す順序と結果は同じ。試したが取り消したもの：フォントの静的キャッシュ（既存のフォント順序テストと競合）、開始時の測定を2フレームに分割（準備完了が1フレーム遅れ、既存の時刻テストと競合）。`tests/Nova.Runtime.Tests` で 1,324件成功・0件失敗。アイドルベンチ（`--idle-bench`）では1万フレームの割り当てが、Nova1人・幅1.25で 2.8MB→0、幅0.25で 7.2MB→0、15人・幅0.25で 108MB→0。処理時間は約3〜8分の1。ユーザーが容量を空けた後（空き861MB）、本体を `-p:CopyToAmongUs=false` でReleaseビルドし、エラー0、C#警告19件（従来と同じ。Novaの警告なし）。`bin/Release/net6.0/TownOfHost-Shell.dll`（SHA-256 `794D017FA2D5148D0E2AF2FCFA0715A9D2987C985DF2A72F65246737832E56D1`）を `C:\Program Files (x86)\Steam\steamapps\common\Among Us\BepInEx\plugins` へ配置し、ハッシュ一致を確認。配置前のDLL（タイガー修正版、SHA `13C77B63…71ED`）は `../nova-lightweight-20261124/backup/` に保存。**配役時のクラッシュが直ったかは実機で未確認。** |
| MOD全体の軽量化（初版の記録） | ユーザーの依頼。`Modules/PlayerCatch.cs` のみ変更。`PlayerCatch.AllPlayerControls`（526箇所）/ `AllAlivePlayerControls`（295箇所）は、呼ばれるたびに `PlayerControl.AllPlayerControls.ToArray()` でIl2Cppのリストを全員分コピーしていた。毎フレーム・プレイヤーごとに何度も呼ばれている。これを、同じフレーム（`Time.frameCount`）の間は1回コピーした配列を使い回すように変更した。リスト本体・人数・最後の要素（いずれもIl2Cppの `Pointer` で比較）が変わったら、同じフレーム内でもすぐ作り直す。生存・null・PlayerId≤15の判定は従来どおり毎回その場で行う。同じフレーム内の差し替えで古い一覧が返る問題は次の行で修正済み。メインスレッド外（`Time.frameCount` が例外）では従来どおり毎回コピーする。`GetPlayerInfoById` も、`GameData.Instance.AllPlayers.ToArray()` のコピーをやめ、前から順に探すループにした（最初の一致を返す点・データが無い時に例外になる点は同じ）。Releaseビルド：エラー0、警告は従来と同じ。**このファイルを使うテストプロジェクトはなく、実機での動作確認・効果測定は未実施。** この初版は未配置。 |
| PlayerCatch一覧キャッシュの修正（未コミット・Steam配置済み） | ユーザーの確認依頼。前回版の判定は「フレーム・リスト本体・人数・最後の要素」だけだったため、**同じフレーム内で人数と最後の要素が変わらず、途中のプレイヤーだけが入れ替わった場合（インデクサ代入、途中の削除と同じ位置への挿入など）に、古い一覧を返す不具合があった**。修正内容：判定に使い回しの条件として、Il2Cpp `List<T>` の変更回数 `_version`（追加・削除・挿入・インデクサ代入・Clearのたびに増える）を加え、最後の要素の比較はやめた。判定とコピーを新規 `Modules/FrameSnapshotCache.cs`（`FrameSnapshotCache<T>`）に分け、`Modules/PlayerCatch.cs` の `PlayerSnapshot()` から使う。一覧取得の従来の挙動（生存・null・PlayerId≤15の判定は毎回その場で、メインスレッド外は毎回コピー）は維持した。回帰確認：新規 `tests/PlayerCatchSnapshot.Tests`（`FrameSnapshotCache.cs` をリンク。偽リストは `_version` の動きを再現）で10件成功・0件失敗。内容は、同一フレームでの使い回し、途中の入れ替え（人数・最後の要素は同じ）、途中の削除と再挿入、削除後の追加、Clear後に同じ人数まで追加、次フレーム、別リスト、空リスト、キャッシュしないコピー、ランダム操作2万回で毎回コピーした結果と一致。Releaseビルド（`-p:CopyToAmongUs=false`）：エラー0、警告は従来と同じ件数（`List._version` がMODから参照できることもビルドで確認）。**実機での確認と効果測定は未実施。** 既存の回帰テストを再実行して10件成功。ユーザー指示により、最新のRelease DLLをゲーム終了中にSteamへ配置し、SHA-256の一致を確認した。配置前のノヴァ軽量化版は復元用に保存済み。コミット・pushは未実施。 |
| ノヴァのベンチマーク | 既存の未コミット変更として `tests/Nova.Runtime.Tests/Program.cs` の `--idle-bench` 分岐と、未追跡の `IdleBench.cs` があった（作者は不明。今回は変更せず利用のみ） |
| 検証コード | 未追跡の `tests/MadAlternate.Runtime.Tests/` 内のプロジェクト・`Program.cs`・`Stubs.cs`、計3ファイル |
| HUD回帰テスト | `tests/GameplayHudUi.Tests/` にバグ報告の回帰テストを追加。製品コードの抽出・由来情報、ログとHTTPクライアントの参照、スタブを更新。 |
| 引き継ぎ文書 | 追跡済みで変更ありの `AGENTS.md` と、未追跡の `docs/AI_HANDOFF.md` |

## 完了済みの検証と配布

| 項目 | 確認内容 |
| --- | --- |
| Windowsの最終Releaseビルド | エラー0件、既存警告19件 |
| 戦闘・タスク関連の自動テスト | 最終DLLの作成時にタイガーのPC模擬テスト11件、マッドアルターネイトのPCテスト33件成功。タスク上限の変更時に設定IDの回帰テスト41件成功 |
| Steam配置済みDLL | 現在はバグ報告フォームのログ添付表示修正版。Steam版 `BepInEx/plugins/TownOfHost-Shell.dll` のSHA-256はビルド品と一致し、`01C79D6830EBEF406274C432C52A7A557F1B0DCE0D445782870AD7324FC82ABB`。直前のDLLは `../bug-report-attachment-ui-20261124/backup/TownOfHost-Shell.dll` に復元用として保存し、SHA-256の一致を確認。 直前はPlayerCatch一覧キャッシュ修正版（ノヴァ軽量化を含む）。`C:/Program Files (x86)/Steam/steamapps/common/Among Us/BepInEx/plugins/TownOfHost-Shell.dll` のSHA-256はビルド品と一致し、`D7346B642FFFCD0BDA713F5EC902385191B70762694D4F2ECA11F09BDDA209F0`。PlayerCatch配置前のノヴァ軽量化版は `../playercatch-20261124/backup/TownOfHost-Shell.dll` に保存（SHA-256 `794D017FA2D5148D0E2AF2FCFA0715A9D2987C985DF2A72F65246737832E56D1`）。実ゲームでは、入退室、試合開始、生存一覧の更新、軽量化の効果が未確認。 過去の記録。IME確定修正版を `C:/Program Files (x86)/Steam/steamapps/common/Among Us/BepInEx/plugins/TownOfHost-Shell.dll` に配置。当時のSHA-256はビルド品と同じ `DAB3644E819CE296AD615AEAEDE4CF698910FF1AC9875309D5480F9C492D7828`。旧DLLと一致する `bug-report-input-20261124/release/TownOfHost-Shell.dll` を復元用に保持（SHA-256 `0EEB620A913770DEA704E3470BA703D937CF8A78A0C4B9FB0F61BCA3D501441F`）。報告用設定ファイルのハッシュは配置前後で一致。 |
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

- ノヴァ（2026-11-24 21:14〜21:19のホストLogOutput.logを調査）：非ホストのAndroid（「あ」）がノヴァだった試合（21:14:09開始）で、21:14:32.98に `NOVA_VISUAL_STATUS status=1`（準備完了）となった後、21:15:01に「あ」が `ExitGame` で切断した。その28秒間、ホストには `PhantomButton Use` / `CheckVanishRPC` / `NOVA_CHARGE` が一度も届いていない（正常だった21:16:47開始の試合では、押すたびにこの3つが記録される）。ホストのノヴァ処理もその間は何も送っていない。つまり、ボタンを押した時の障害は**Android側でホストへ届く前に起きた**もので、ホスト側の処理量は直接の原因ではない。次は、Android側の画面に出たエラー文と、可能なら端末のログ（logcat）を入手して調べる。ホスト側ノヴァのさらなる軽量化は、この根拠がないため保留した。軽量化版のDLLは配置済み。Android側の表示は「通信エラーが発生しました」（ユーザー確認）。USB接続のPixel 10aのlogcatを読み取りだけで確認した（端末の操作・変更なし）。結果：Among Usのプロセス（pid 7736）は21:11から21:15:16以降まで同じで、アプリ自体は落ちていない。21:14:30〜21:15:16にWi-Fi・接続の切断記録はなく、正規版はUnityのログを出さないため、ゲーム内の原因は端末ログからは特定できない。結論：アプリのクラッシュではなく通信の切断。原因はホスト・端末の両ログで特定できていない。再発したら、押した時刻と画面の状態を記録し、直後にlogcatとホストのログを保存する。
- ノヴァ：配役時にホストが落ちるかを実機で確認する。再発した場合は、その時刻の `LogOutput.log`（`NOVA_STARTUP_WAIT` / `NOVA_PREPARE_QUEUED` / `NOVA_VISUAL_STATUS` 付近）と Player.log を保存して原因を調べる。開始時の測定（`PreparePacked` と `PlanBeam`）の1フレームへの集中が残っている点は、ログで確認できてから対処する。

1. **引き継ぎの読み取り確認**。新しく利用するツールでは、この共通開発元のパス・ブランチ・HEADと未公開差分を読み取り専用で確認する。
2. **Windowsの実ゲーム確認**。次回は非公開で、ルーラー・チューナーの延長中にタイガーが捕食直後にキルできるかを確認する。マッドアルターネイトは3種のタスク上限10、身代わりと「殉死」、次の会議での失効を確認する。
3. **旧コピー固有の機能**。二つ名、報告、リレー、旧移植の差分を一括で移す作業は未実施。必要な機能だけ、依頼された範囲で履歴と差分を照合する。
4. **Android**。別プロジェクト `C:/Users/Owner/.codex/visualizations/2026/09/18/01a0b3fc-0be8-7c71-b28d-f0568f4338bf/toh-shell-android` に保管。Starlight優先でStaff承認待ち、Androidホストは全員OFF。次工程はAndroid側の `docs/STARLIGHT-BODY-PORT-ja.md` と `docs/STARLIGHT-PARTICIPANT-DRAFT-ja.md` を参照する。
5. **ゲーム内のバグ報告**。Steam版フォームDLLとこのPC用の受付設定は準備済み。許可された既存フォーラムへの動作テスト1件は、フォームと同じ中継APIを使う検証ツールから送信し、実投稿を確認済み。投稿記録は親の `bug-report-form-20261124/cloudflare-deploy/discord-test-result.json`。テストの送信許可は消費済みで再送しない。実ゲーム画面での入力・クリックは確認していない。別PCに試験導入する過程では[中継の導入手順](../tools/bug-report-relay/README-ja.md)を参照。Nightly RepairはBot投稿を自動修正の対象から外す既存仕様を維持する。

## 次のAIに渡す短い文

> Windows版TOH-Shellの共通開発元として `C:/Users/Owner/.codex/visualizations/2026/09/18/01a0b3fc-0be8-7c71-b28d-f0568f4338bf/mad-alternate-20261123-133208-111/repo` を開いてください。まず `AGENTS.md` と `docs/AI_HANDOFF.md` を読み、読み取り専用でパス、ブランチ、HEAD、未公開の役職修正を確認して報告してください。その後の作業では既存差分を保全し、終了時に同じ引き継ぎメモを更新してください。
