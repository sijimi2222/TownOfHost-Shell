# YouTube Google連携・Windows保護保存（ローカル開発版 v2.2.3）

2026-11-13。認証UI・一時ループバック・DPAPI保存を実装し、開発者PCでの公式API取得・ゲーム内表示成功は利用者から報告された。今回の自動検証ではGoogleへ接続していない。他のホスト環境・一般配布の認証情報提供方式・バックエンドと参加希望受付の規約適合は未確認であり、一般公開の準備完了を意味しない。[一般ホスト向け監査・公開前手順](youtube-general-host-readiness.md)を参照する。

## 初回に開発者が用意するもの

1. 正当に管理するGoogle CloudプロジェクトでYouTube Data API v3を有効にする。今回はプロジェクト作成やGoogleへの接続を実施していない。
2. OAuthの同意画面・アプリ名・連絡先・利用規約/プライバシーポリシーを整備する。利用者の範囲に応じてテストユーザー登録や必要なOAuth審査を行う。
3. OAuthクライアント種別を **Desktop app** にする。Web application、サービスアカウント、OOB方式を使わない。
4. `youtube-oauth-client.example.json` を参考に、`client_id`だけのJSONを、ゲームの `BepInEx/config/TOH-Shell/YouTube/oauth-client.json` に用意する。Googleから取得した完全なclient JSONをそのまま置かず、client_idだけを転記する。この設定は非秘密情報であり、トークンやAPIキーを書くファイルではない。その他のフィールドは拒否する。
5. クライアントIDは公開クライアントの識別子で、利用者の秘密トークンとは異なる。ただし、YouTubeのAPI Credentials提供制限との整合確認なしに一般配布してよいという意味ではない。現在の「実値をGit・公開配布物へ含めない」条件では、初回設定を不要にする提供方式が未決定。開発者のJSON・DPAPIファイルを他のホストへコピーしない。DLL内には実値を埋め込んでいない。

[Google Desktop OAuth仕様](https://developers.google.com/identity/protocols/oauth2/native-app)に基づく。要求権限は `https://www.googleapis.com/auth/youtube.readonly` のみ。これはライブチャットだけに限定された権限ではなく、YouTubeの読取権限である。email/profile/openid、投稿・削除・アップロード権限は要求しない。Googleの同意画面で内容を確認すること。

Desktopアプリは公開クライアントで、client_secretを完全な秘密として保証できない。PKCE S256とstateは常に維持する。Google資料ではclient_secretは省略可能だが、実機でTokenExchange / ClientSecretRequired / HTTP400が確認された。同じDesktopクライアントのsecretを明示的に保護取込した場合だけ、トークン交換と更新のHTTPS POST本文へ送信する。自動推測・自動再試行・DLLへの埋め込みは行わない。

## client_secretを要求された場合の安全なローカル設定

1. ゲームを終了する。Google Cloudの既存DesktopクライアントからJSONをダウンロードする。新しいクライアントを作成し直す必要はない。内容・secret・Client IDをチャットやログに貼らない。
2. GoogleのJSONは`installed`形式で、現在の`oauth-client.json`と同じclient_idである必要がある。異なるIDは取込ツールが拒否する。平文secretを既存の`oauth-client.json`へ追記しない。
3. ソースに付属する`tools/Import-YouTubeDesktopClient.ps1`をWindows PowerShellで実行する。コマンドに渡すのはファイルパスのみ。以下のパスは各自の保存先へ変更する。

```powershell
powershell.exe -NoProfile -File "C:\path\to\TOH-Shell\tools\Import-YouTubeDesktopClient.ps1" -GoogleClientJson "C:\private\downloaded-desktop-client.json" -ClientConfiguration "C:\Program Files (x86)\Steam\steamapps\common\Among Us\BepInEx\config\TOH-Shell\YouTube\oauth-client.json"
```

4. 既定の保存先は`%LOCALAPPDATA%/TOH-Shell/YouTube/client-secret.dpapi`。現在のWindowsユーザー用DPAPIと、そのユーザーのみのACLで保存し、client_idも暗号化内に結び付ける。既存の設定・oauth.dpapi・oauth.disabledは編集しない。既存secretの置換は明示的な`-ReplaceExisting`が必要。
5. 取込後にゲームを再起動し、新しいGoogle連携を開始する。前回の認証コードやタブを再利用しない。ShowCommentsは自動でONにならない。
6. ダウンロードJSONは平文のため、共有・配布・Git登録をしない。不要なら利用者自身で安全に削除し、Google Cloudから再取得できることを確認する。

secret未設定なら従来のsecretなし経路を使う。暗号化ファイル破損・別Windowsユーザー・異なるClient IDなら安全に停止し、secretなしへ黙って戻らない。DPAPIは保存時の保護であり、同じユーザーの不正プロセスや管理者に対する完全な秘密性、利用中の管理文字列の完全消去は保証できない。secretはユーザーのアクセストークンとは別のクライアント設定であり、Google連携解除では消さない。不要ならゲーム終了後に`client-secret.dpapi`だけを明示的に削除する（次回secret要求時には再取込が必要）。

## 利用者の操作

- 既存の **Stream** 画面から **Google / YouTube連携** を開く。
- **Google連携開始** を押す。システムの標準ブラウザーが開き、Googleで使用するアカウントを選んで同意する（`prompt=consent select_account`）。GoogleパスワードをMOD内に入力する欄はない。通常の保存済みトークン更新ではブラウザーを開かない。
- 応答受信後のブラウザー表示は「受信した」という案内だけ。トークン交換と保存が終わったかはMODの連携状態で確認する。
- 保存に成功すると連携情報ありと表示する。通常は以後の起動でもこのWindowsユーザーの保存情報を復元し、必要時に自動更新する。
- 既存のコメント表示をONにした通常利用では、ライブURLの貼り付けで取得する。ShowCommentsは利用者が明示的に切り替える。連携完了でも勝手にONにしない。
- **認証をキャンセル**、戻る/画面を閉じる操作で未完了の認証を止める。実行待ちの段階でもキャンセルし、画面を離れてから遅れてブラウザーを開かせない。
- 再連携は、新しい認証の検証・保護保存が成功するまで旧情報を維持する。拒否・中断・通信失敗で旧grantを消さない。保存失敗時は安全停止し、保護保存成功後のキャンセルは新grantを削除しない。旧情報のGoogle側での有効性は次の利用時に確認する。
- クライアント設定が未設定だった場合は、JSONを置いてから連携開始を押せば読み直す。すでに設定済みのclient_idを別のIDへ変更する場合は、解除してゲームを終了し、設定変更後に再起動する。

初回同意後のトークンが有効なら再ログインを要求しない設計だが、ユーザーの解除、管理者の制限、トークン失効、Googleプロジェクトの設定等により再同意が必要になる。永続的な認証を保証するものではない。

## 保存と更新

- 保存先：`%LOCALAPPDATA%/TOH-Shell/YouTube/oauth.dpapi`
- Windows DPAPIの**現在のユーザー**で暗号化。マシン全体向けの保護フラグは使わない。通常、同じPC/Windowsプロファイルで復元し、他PCやプロファイル再作成では再連携が必要。
- 専用フォルダーのDACLを継承なし・現在のWindowsユーザーだけへ設定する。ACL設定や暗号化に失敗した場合は平文へ切り替えない。
- access/refresh token、期限、client_id、readonly scopeを暗号化して保存。client_idとscopeが違うファイルは使わない。期限前約1分以内になったトークンはコメント取得時に更新し、保護保存に成功してから使用する。
- 起動時の復元はローカルの復号のみ。コメント表示OFFの間に自動トークン更新やAPI取得をしない。
- 暗号化後のデータだけを同じ専用フォルダーの一時ファイルへ書き、flush後に置換する。平文の中間ファイル、cfg内トークン、ログへの認証情報出力は作らない。
- 書き込み前に非秘密の `oauth.disabled` マーカーを置く。保存途中の失敗や削除失敗が残った場合は、次回起動でも旧情報を勝手に復元しない。正常保存/削除が終わった場合だけマーカーを取り除く。
- データは最大64KiB、再解析深度・文字列長にも上限を設ける。保存先が無効・相対パスの場合にゲームの作業フォルダーへ代替保存しない。
- DPAPIは同じWindowsユーザーで動く悪意あるソフトや管理者に対する完全な隔離ではない。OSのログオン環境も保護する必要がある。管理メモリ上の文字列を完全にゼロ化できるとは主張しない。明示的なbyte配列と復号後ネイティブ領域は消去する。

[Microsoft DPAPI仕様](https://learn.microsoft.com/en-us/windows/win32/api/dpapi/nf-dpapi-cryptprotectdata)を参照。

## エラーと解除

- API側からの認証拒否やクォータエラーはコメント枠だけでなく連携画面にも表示する。保存情報ありという表示だけで実接続確認済みとは扱わない。APIで認証を拒否された場合は停止し、必要に応じて再連携する。
- Google側で失効した更新トークンは使わず、端末保存を消して再連携を案内する。削除できない場合も処理を止め、削除失敗を表示する。
- 一時的なHTTP 5xx・通信断・認証リクエスト制限で、まだ有効かもしれないrefresh tokenを失効扱いして捨てない。通信断は既存の回数制限付き再試行、クォータ/リクエスト制限は停止して案内する。
- 認証・保存エラーでも非公開API/HTML取得へ戻らない。応答本文や例外の生メッセージを画面/ログへ出さない。
- **連携解除** はコメント受信を止め、コメント/希望者情報と端末保存を消してから、公式 `https://oauth2.googleapis.com/revoke` への失効要求を試みる。トークンはPOST本文に入れ、URLやログへ出さない。
- Google側の解除に失敗した場合、端末内の情報は消去した旨と、Google権限管理画面で確認が必要な旨を表示する。端末削除だけでGoogle側の解除も成功したとは表示しない。
- client設定がない/保存情報を読み取れない場合も端末削除を試み、Google側の状態は権限管理で確認するよう案内する。
- Googleの公式仕様では、プロジェクト単位で他のクライアントや権限へも失効が及ぶため、Shell用に管理するプロジェクトを他用途と不用意に共用しない。画面内にも影響を明記した。[公式の解除説明](https://developers.google.com/identity/protocols/oauth2/native-app)
- **Google権限管理** は利用者が押した時だけ `https://myaccount.google.com/permissions` を開く。

アプリの通常終了では未完了の認証を止めるが、保存済みの許可は消さない。解除は利用者の明示操作で行う。

## ローカル受信の安全性

専用ポートを0指定で確保したIPv4 `127.0.0.1` にだけbindし、その確保済みポートをリダイレクトに使う。全NICへのbind、固定ポートの推測、ポート確保後の再bindは行わない。PKCE S256、乱数state、認証期限10分、1回限りの応答を確認する。

HTTPヘッダー8KiB、コールバックURL長、16リクエスト/認証、各接続3秒を上限とする。Host・パス・GET・ボディなしを確認し、重複Hostや外部Originを拒否。レスポンスにコード/state/tokenを反射しない。no-store、no-referrer、CSPを付け、CORSは公開しない。成功・失敗・取消・ブラウザー起動失敗でリスナーを閉じる。実機のブラウザー/ファイアウォール/セキュリティソフト環境との互換性は未確認。

## APIキー方式

[公式ガイド](https://developers.google.com/youtube/data/live/streaming-live-chat)はAPIキーまたはOAuthでの認証例を示している。既存の `MemoryYouTubeCredentials` と `YouTubeCredential.IsApiKey` により、`videos.list`とgRPCの両方へ `x-goog-api-key` ヘッダーを付ける経路を模擬テスト済み。

今回はAPIキーの設定UI/永続化を追加せず、OAuthトークン保存先へAPIキーを混入させることも拒否する。公開DLLへのキー埋め込みなし。キーを隠すためのバックエンドは規約確認前のため構築しない。利用者ごとにCloud/APIキーを作らせる方式は、URLだけという目的に合わないため既定にしない。

## 未完了の確認

開発者PCでの認証・公式API取得・ゲーム内表示成功は利用者報告による。今回追加したアカウント選択、別ホスト環境、実トークン更新・解除の挙動は未検証。自動テストは合成トークン・模擬HTTP応答と127.0.0.1だけ。Windows DPAPIの実呼出しは独立したテストフォルダー内で検証し、実ユーザーのGoogle認証データは扱わない。

参加希望は人数・表示名、ID重複排除、200件/30件ページの仕組みを維持するが、規約確認待ちゲートはfalseのまま。OAuthに同意しても公開受付が許可された扱いにはしない。マップ投票、Kick、ゲーム内名照合は復活しない。正式なShellの公開プライバシーポリシー・ブランド表記の最終確認、バックエンド/参加受付に関する照会は別工程。
