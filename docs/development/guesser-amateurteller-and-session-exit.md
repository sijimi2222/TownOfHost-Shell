# かけだし占い師の推測修正とロビー終了診断
2026-11-14 / TOH-Shell v2.2.3 / ローカル開発ブランチのみ。

## 結論
ゲッサーの日本語名変換の誤りは特定して修正した。一方、部屋を閉じた際のプロセス終了は、今回のログだけでは根本原因を特定できていない。後者は修正済みではなく、ShellDevelopment専用の診断を追加した段階。ゲーム起動・Steam DLL配置・実Google/YouTube接続・公開・コミット/プッシュは行っていない。

## 1. ゲッサー
- 内部名・正式英語名・翻訳キー：AmateurTeller。日本語名：かけだし占い師。RoleId：179。設定の基点：30100。
- UtilsRoleInfo.FixRoleNameInputの日本語・ひらがな・カタカナ別名が、存在しないBeginnerTellerキーへ変換されていた。その後のGetRoleByInputNameで登録役職と一致しない。
- 変換先だけをAmateurTellerへ訂正。英語名・大文字小文字、既存の数値ID/色指定による対象選択を検証した。新しい役職やID、翻訳、設定は追加していない。
- GuessManagerの成功/誤射判定、射撃回数、対象の死亡・推測禁止条件は変更していない。占い系のゲッサー使用禁止設定は射手の役職に関するもので、この名前変換不良を正当化する条件ではない。AmateurTeller自身にCheckGuessの独自上書きはない。
- 共通解析を使うゲッサー系にも同じ訂正が反映される。実際の会議・射殺通信の確認は未実施。

実コードの該当メソッドを抜き出してコンパイルし、同一の32テストを変更前の隔離ソースと修正後で比較した。変更前23成功・9失敗、修正後32成功・0失敗。1,516件の既存の英語/日本語入力マッピングと比較し、対象以外の対応を維持した。

## 2. 部屋を閉じた際の終了
今回の再現ログは2026-11-14 10:46の起動回。認証値・利用者名・コード・URL・コメント本文は本書へ転載しない。

| 最後に確認できた処理 | 根拠 |
| --- | --- |
| 部屋退出、役職の解放 | LogOutput.logの10:46:37.40、Dispose ActiveRoles |
| シーン遷移とHUD更新 | Unity Player.logのExitGame、OnActiveSceneChange、未使用アセット解放。HeaderLayoutのphase=0 |
| タイトル画面の再構築 | LogOutput.logの10:46:37.96、SEASIDE_STANDARD_MENU START、10:46:38.18、MENU_VIEW_BUILT |
| その後 | 最後の記録は10:46:38.26のバージョン取得404。復帰後の最初のNATIVE_MENU_TICKは記録されていない |

これらは最後に観測できた位置であり、正確なクラッシュ位置や原因の証明ではない。Unityの重複GameManagerCreator警告とバージョン取得404があるが、それだけを原因とは判断しない。NullReferenceException等の未処理例外やネイティブクラッシュのスタックはこの起動回のログにない。
取得可能なWindows Applicationイベント、WER、ローカルのSentry/SentryNativeも確認したが、この起動回の原因を示す新しい障害情報・ダンプは確認できなかった。過去のWERは今回の証拠と混同しない。SentryNativeの今回のファイルは0バイト。認証関連の保存ファイルは診断のために解析していない。
この起動回のYouTube取得は10:46:32にHTTP200の動画情報応答の後、LiveEndedで停止している。これを部屋終了時のクラッシュ原因とは断定できない。新規の待機ロビーでの再現であり、Nova発動を原因とする根拠も得られていない。通常のアプリ終了要求、管理例外、ログに残らないネイティブ障害等の区別が残る。

## 3. 追加した開発版専用診断
Development/DebugController/SessionExitDiagnostics.cs。既存のビルド分離規則で公開Releaseには含めない。マーカーはSESSION_EXIT_V1、ログタグはSessionExit。
- ExitGame / DisconnectInternalの入口・戻り。
- CustomNetObject.Reset、StreamerInfo.StopNetwork、CustomRoleManager.Dispose、HudManager.OnDestroyの開始・戻り。
- シーン変更、MainMenuManager.Start、復帰後のLateUpdateの戻り。
- ShellメニューのQuit呼出しとApplication.quittingを区別。
- 管理例外は固定分類だけ記録し、元の例外をそのまま返す。例外の握りつぶしや終了処理の順序変更はしない。
- 退出ごとに同一段階を1回、最大24行。経過時間・固定段階・理由の列挙値・管理対象数・コメント状態の数値だけを記録。URL、利用者名、ルーム情報、認証値、リクエスト本文、例外メッセージは記録しない。
- 認証・通信の開始、送信、強制終了、終了処理の迂回、Unityオブジェクトの追加破棄は行わない。イベント登録は一度だけ。

実際のIL2CPP参照DLLで対象メソッドの存在・オーバーロード・引数名を照合済み。実ゲームでのHarmony適用とネイティブ終了の観測は未確認。TrackingCompleteは2回のメニューLateUpdateを観測したという意味で、操作可能・クラッシュ解消の保証ではない。OSによる強制終了やネイティブ障害では診断が残らない場合もある。

## 4. 検証
- 全回帰テスト：47プロジェクト、6,472成功・失敗0。最終の変更に関係するプロジェクトは最新版で再実行し、変更していないプロジェクトの成功結果と合わせて集計。
- 新規の名前解析32件、終了診断18件、実コメント処理の模擬終了4件。コメントON/OFF、複数経路からの重複停止、20回の再作成、停止後の古いコールバックを確認。
- 既存のNova/CNOは1～15人相当・複数所有者・生成中失敗・通信終了・二重回収を含む模擬試験で確認。新しい診断テストを実ネットワークの多人数試験としては扱わない。
- Nova.Runtime 1,324、Stability.Runtime 246、HUD 204、PetId/終了処理54、陰陽師137が成功。
- Release / ShellDevelopmentビルド：両方0エラー。両方20警告で、既存C#警告に追加なし。
- ビルド分離・既存のNova/CNO/通信IL維持の監査：別途22件成功。ReleaseにSessionExitDiagnostics/SessionExitTraceが存在せず、開発版だけに含まれることをDLLメタデータで確認。
- 埋込みPDBのチェックサムで修正ソースとDLLを照合。YouTube公式API/OAuth/DPAPI、正常gRPC終端、Nova、陰陽師、HUD、PetId対策を維持。公式サイト・Steam DLL・認証設定の無変更は別途ハッシュ照合する。

## 5. 実機で残る確認
1. 別途許可された後にこのShellDevelopment診断版を配置し、ログにSESSION_EXIT_V1があることを確認する。今回配置はしていない。
2. 待機ロビーを作成して終了し、再起動前のLogOutput.log / Player.logを保存する。まず不要な外部接続を行わない条件で確認する。
3. seqごとの最後のstage、LifecycleException、MenuQuitInvocation、ApplicationQuittingの有無を照合する。通常Quitマーカーがないことだけでネイティブクラッシュと断定しない。
4. 診断に基づいて次の修正を絞り込み、その後に参加者あり・試合中・ロビー再作成・コメントONでの確認へ進む。現時点でこれらが成功したとは扱わない。
5. イビルゲッサー等で日本語名とAmateurTellerを指定し、正解・誤射・設定による禁止・戻りを確認する。

## 主な変更ファイル
- Modules/Utils/UtilsRoleInfo.cs：別名の翻訳キー1行。
- Development/DebugController/SessionExitDiagnostics.cs：開発版だけの終了経路観測。
- tests/Guesser.RoleNames.Tests/*：実コード切片、数値ID、翻訳対応、変更前マッピングの比較。
- tests/SessionExit.Diagnostics.Tests/*：終了観測・重複抑止・例外非抑制・ログ漏えい防止。
- tests/Streaming43505.Tests/LobbyExitTests.cs、Lifecycle.cs：実コメント処理の模擬終了テストと呼出し。
- 本書。その他の既存未コミットソースは保護する。

変更前バックアップ、再現テスト、ビルド、照合記録はプロジェクト外のguesser-lobby-fix隔離領域へ保存。生ログは非公開の検証用スナップショットで、ソースや公開成果物へ含めない。
