# YouTube公式API受信基盤（v2.2.3ローカル開発版）

2026-11-12。認証UI・Windows DPAPI保存を追加した（[操作・設定・保護方式](youtube-oauth-user-guide.md)）。公開実装の規約承認・実通信の動作保証を意味しない。ShowCommentsの初期値とSteam側の保存値はOFFを維持する。以下のAPI・認証コードは模擬HTTP応答でのみ検証した。

## 実装した経路

1. 既存のURL検証から11文字の動画IDを得る。URL入力・保存・追跡パラメータ除去を維持。
2. `IYouTubeCredentialProvider`から資格情報を取得する。初期状態は`MissingYouTubeCredentials`で、メニュー/HUD初期化時に`YouTubeAuthService`へ接続する。認証情報がなければHTTPを一度も送らず、コメント枠へ初期認証未設定を表示する。
3. `GET https://www.googleapis.com/youtube/v3/videos?part=liveStreamingDetails&id=...`で`activeLiveChatId`を取得する。
4. 公式gRPC `https://youtube.googleapis.com/youtube.api.v3.V3DataLiveChatMessageService/StreamList`をHTTP/2で呼ぶ。
5. 公開されたprotobufの必要フィールドを上限付きで読み、既存のメインスレッド用コメントキューへ渡す。現在は通常のテキストコメントが対象。Super Chat・投票・バッジ等の再現は未実装。

[公式ストリーミングガイド](https://developers.google.com/youtube/data/live/streaming-live-chat)、[videos.list](https://developers.google.com/youtube/data/reference/rest/v3/videos/list)、[streamList](https://developers.google.com/youtube/data/live/reference/rest/v3/liveChatMessages/streamList)の公開仕様を参照。streamListは初期履歴を複数の応答として送るため、最初の1応答だけを履歴扱いする設計にはしない。

`ChatData`の旧取得実装、`StreamCommentParser`、HTML/ytcfg/continuation解析、非公開API送信を撤去。保存済みの旧設定でも復活せず、公式APIエラーから旧方式へ戻る分岐はない。旧実装の出典クレジットは保持する。

## gRPC・IL2CPP依存関係

このプロジェクトはBepInExの.NET 6ホストで動作し、UnityのMono通信スタックを使う構成ではない。`SocketsHttpHandler`、HTTP/2、標準TLS検証、gRPCの5バイト長ヘッダーとprotobufの限定的な読み書きを使用する。新しいNuGetパッケージ・ネイティブgRPC DLL・コード生成ランタイムは追加していない。

[.NETの対応条件](https://learn.microsoft.com/en-us/aspnet/core/grpc/supported-platforms?view=aspnetcore-6.0)と[gRPCのHTTP/2仕様](https://github.com/grpc/grpc/blob/master/doc/PROTOCOL-HTTP2.md)に基づく構成。独自実装しているのは公開protobufの限定コーデックであり、Googleクライアントライブラリの完全な代替ではない。公式サービスとの相互接続、Windows/BepInEx内のHTTP/2・ALPN・TLS・トレーラー受信、プロキシ環境は**実機未検証**。HTTP/1.1・gRPC-Web・非公開APIへの自動切替は行わない。

REST取得12秒、ストリームヘッダー15秒、1呼出し最大20分またはトークン寿命まで。無言のライブを通信障害と誤判定する短い受信アイドル期限は設けず、取消と呼出し寿命で待機を制限する。期限による正常更新は5秒待って再開し、通信エラー回数に含めない。ヘッダー用タイマーを長時間の本文受信へ持ち越さない。再接続では最後の非空pageTokenを再利用する。ネットワーク障害だけ最大4回の試行（間隔5/10/20秒）。認証・権限・クォータ・終了・不正応答では停止し、毎フレーム再要求しない。再開は表示OFF→ONまたはURL変更後の新セッションで行う。

大量の初期履歴は応答ごとに最新30件だけを表示キューへ渡す。新しいコメントをこの履歴制限で捨てることはしない。gRPC/JSON本文は各2MiB、キュー512件、表示履歴30件、重複IDキャッシュ512件に制限。表示文字はTMPタグとして解釈させない。本文・URL・チャンネルID・アクセストークン・エラー本文を診断ログへ出さない。ログは固定理由コードと試行数のみ。バッファ超過も安全停止する。

## OAuth認証UIと保護保存

認証開始・状態・解除・エラー/クォータ案内を専用画面へ追加した。システムブラウザーと127.0.0.1一時ポートでPKCE応答を受け、同じWindowsユーザーのDPAPIと専用フォルダーACLでトークンを保護する。次回起動でローカル復元し、コメント取得が必要なときだけ更新する。ShowCommentsを勝手にONにしない。

詳細・保存場所・初回Google Cloud設定・解除の影響・実機未確認事項は[新しい手順書](youtube-oauth-user-guide.md)を参照。設定/トークン/クライアントの実値は今回作っていない。実認証・実接続は未検証。バックエンドは`IYouTubeChatSource`で差し替え可能だが、構築/公開しない。

## 参加希望・UI・データの扱い

人数とYouTube表示名だけ、チャンネルIDで重複排除、上限200件、30件ずつのページ表示、受付開始/終了/一覧初期化を維持した。表示名が同じ別チャンネルは別件。マップ投票・Kick・ゲーム内名照合・自動入室は撤去済みのまま。

`ParticipationPolicyReviewed`の既定値falseで、実コメントからの受付は停止する。模擬テストだけtrueを注入する。UIにも規約確認待ちを表示。過去履歴は公開時刻と接続/受付開始/初期化時刻、キューの受付世代で除外する。不明な公開時刻は受付には使わず、コメント表示だけに使う。

- 配信URLと参加/撤回キーワードは従来のBepInEx設定へ保存する。トークン・チャット本文・希望者一覧はその設定へ書き込まない。
- コメント本文/表示名は受信キュー・直近30件の表示用メモリのみ。チャンネルIDは重複防止・希望者管理に使い、画面には出さない。
- URL変更・切断・アプリ終了で表示/希望者を破棄。表示OFFで通信とコメント履歴を停止/消去し、受付は止める。終了した受付一覧は手動初期化または退出まで残せる。
- 認証失効時はコメント/希望者を消す。希望者一覧の初期化はコメント枠位置のリセットと別。ローカルの一覧削除でYouTube側の投稿が消えるわけではない。
- 今回の受信経路はコメント/希望者をログ・ファイル・Webhook・OBS配信用JSONへ保存/転送しない。画面を配信・撮影すると視聴者に名前と本文が見えるため、利用者への説明が必要。
- API通信とトークン交換先はGoogle公式だけ。ゲームのRPCへYouTubeコメントを追加していない。他プレイヤーの参加資格・通信は変更しない。
- Stream設定画面にYouTube利用規約/Googleプライバシーへのユーザー操作リンク、保存/削除/画面共有範囲の短い注意書きを追加。コメント枠にYouTube出典を表示。**正式なShellの公開プライバシーポリシーURL、削除窓口、Google権限解除への導線、ロゴを含むブランド指針の最終確認は未完了。**

## 公開前の規約上の停止点

[YouTube Developer Policies](https://developers.google.com/youtube/terms/developer-policies)のIII.I.15について、APIデータを中継するバックエンドが許可されるとの確認は得ていない。APIを公開する/クォータを分散して制限を回避する実装はしない。コメントによる希望者受付は単なる一覧でもコメントへの誘因の評価が未確定。強制参加/Kick撤去をもって適合と断定しない。公開利用は照会結果と利用条件の確認後に判断する。

## 次の順番

1. 開発者が公式窓口でバックエンド/希望者一覧の扱いを確認し、Cloudプロジェクトの管理主体・クォータ・プライバシー方針を決める。
2. 直接OAuthを使う場合はDesktopクライアントと同意画面を準備する。秘密情報をチャットやGitへ貼らない。
3. 実装済みの認証UI・一時ループバック・DPAPI保存/解除を、設定を用意した上で実機確認する。
4. ユーザーが操作できる時に明示的同意で初回認証し、公開テストライブを使ってHTTP/2/TLS、Unicode、削除/終了、再接続を確認する。
5. Among Us上で入力・表示・UI配置・通常プレイ負荷を確認する。規約確認前の一般公開や参加受付運用はしない。

今回、実YouTube API通信、Google認証、Cloud作成、サーバー公開、ゲーム起動、Steam DLL配置、Git変更操作は行っていない。
