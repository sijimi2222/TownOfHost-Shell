# YouTube公式APIの通信診断（ローカル開発版）

## 範囲

取得方式・公式エンドポイント・Bearerヘッダー・protobufフィールド・HTTP/2必須条件・タイムアウト既定値・再試行条件は変更しない。原因未確定の切断を解消したとは判断しない。非公開API・HTML取得・別APIへの自動フォールバックはない。

公式仕様との照合先：
- https://developers.google.com/youtube/data/live/streaming-live-chat
- https://developers.google.com/youtube/v3/docs/videos/list
- https://github.com/grpc/grpc/blob/master/doc/PROTOCOL-HTTP2.md

## ログ

BepInEx/LogOutput.log の `YOUTUBE_CHAT` を確認する。OAuth更新は従来の `YOUTUBE_OAUTH` に出る。BepInExへ直接記録し、Unity APIや画面へのログ表示には依存しない。ログが無効なら記録されないが、通信動作は変わらない。

```text
YOUTUBE_CHAT stage=StreamRequest code=NetworkFailure http=0 grpc=unknown httpMajor=0 exception=HttpRequest cause=Socket elapsedMs=10000 totalMs=10200 attempt=0 retryDelaySec=0 reason=NetworkInterrupted socket=HostNotFound
```

この例は架空。http=0 / grpc=unknown は未取得であり成功を意味しない。exception/cause/socketは既知の型・コードのみ。未知の型や不正enum値はUnexpected/Unknownに固定し、生の例外名・Message・ToString・Dataは記録しない。InnerExceptionは最大4段階、SocketErrorは最大5例外を確認する。

## 段階

- Credential：保存済みアクセストークンの取得・必要時更新・有効性検査。Started/Completed。更新HTTPはYOUTUBE_OAUTH側を併せて確認。
- VideosRequest：videos.list GETの開始とヘッダー受信。Bearerトークンをヘッダーへ付け、part=liveStreamingDetailsとURL由来の動画IDを送る。IDもURLもログへ出さない。
- VideosResponse：上限付きJSON受信の開始・完了・HTTP拒否。HTTPステータスを記録。
- ActiveLiveChatId：JSONからチャットID取得。Completedなら取得成功。NotLive/LiveEnded/ChatUnavailable/NotFound/InvalidResponseを固定reasonで区別。IDそのものは出さない。
- StreamRequest：StreamList POSTの接続開始・完了。HTTP/2 RequestVersionExactを維持。接続に失敗してHTTP応答未取得ならhttp=0。
- StreamHeaders：HTTPステータス・HTTPバージョンを記録。HTTP/2とgRPC Content-Typeが確認できた場合だけHttp2Established。不一致はUnsupportedTransport。
- GrpcStatus：headersまたはtrailersのgrpc-status（0～16）を記録。GrpcOk/GrpcRejected/MissingGrpcStatus。grpc-messageやレスポンス全文は出さない。
- Receive / Parse：最初のフレームの受信・protobuf解析の開始・成功。後続フレームの失敗も必ず記録する。通常成功ログは最初だけで、コメント数に比例して増えない。
- Retry：失敗後の回数と待機秒。NetworkInterruptedのみ最大4試行、待機5/10/20秒という従来条件を維持。正常な呼出し期限・トークン期限の更新はRenewalで区別する。
- Stop：RetryExhausted（4試行）、TerminalFailure（認証・権限・クォータ・終了等）、Cancelled（利用者のOFF/切断等）を区別する。

elapsedMsは現在段階の経過時間、totalMsは同じRunAsync呼出し開始からの累計。再試行全体の接続時間ではない。Retry/StopのelapsedMsはその試行の経過時間。値は上限付き整数で記録する。attempt=0は取得層の診断で再試行番号未付与、Retry/Stopのattempt=1～4は所有側の試行番号。

## タイムアウト・終端の読み方

videos.listは既定12秒、streamListのヘッダーは15秒、ストリーム呼出しは最大20分またはトークン期限。ローカル期限切れはTimeout、利用者の取消はCancelled、予定したストリーム再開はRenewal。テストのみ短い期限を注入でき、実行時の既定値は変えていない。

grpc-status=0後のEOFは正常完了とし、EndOfStream（exception=None、reason=None）を記録する。所有側で待機してから取得済みnextPageTokenを引き継いで再接続し、通信エラー回数は増やさない。再開トークンがあり、トークンまたは最新コメントIDに進捗がある場合はRenewalで5秒待機する。進捗なし・空応答・再開トークンなしではIdleBackoffとして5/10/20/40/60秒へ段階的に延長し、以後60秒を上限とする。実際のNetworkInterruptedは従来どおり最大4試行・5/10/20秒待機、認証・権限・クォータ・配信終了は再試行しない。

初回の履歴表示は同じ接続セッション内で一度に限定する。最初の取得呼出しでコメントを受信した後は、再接続時にセッション開始より古い（または公開時刻が分からない）コメントを再投入しない。初回のHTTP失敗でまだコメントがなければ、次の成功時に履歴を表示できる。通常のコメントID重複排除（直近512 ID）・表示30件・受信上限512件は維持し、履歴やIDを無制限に保存しない。OFFや手動切断ではセッションを破棄し、次の明示的な接続は新しいセッションになる。

公開時刻が欠落したコメントは初回表示後の再接続で除外するため、その場合の表示継続は公式応答の公開時刻を確認する必要がある。正常終端の修正と模擬テスト成功だけで、実機のコメント表示成功とは判断しない。

## セキュリティ・負荷

診断のデータ型はenum・数値のみで、任意文字列を受け取らない。トークン、client_secret、認証コード、Client ID、動画ID、チャットID、URL、ヘッダー、本文、コメント、氏名を出さない。未知のGoogle理由文や例外全文も出さない。各ストリームは段階ログと最初の受信成功だけを記録し、後続の通常フレームでは記録しない。診断出力先が例外を出しても通信に影響させない。

## 次回の実機確認

修正版DLLは今回自動配置しない。別途配置後、利用者自身がコメント表示をONにして再現し、失敗前後のYOUTUBE_CHATを確認する。検証後はOFFへ戻す。Google認証は必要な場合だけ利用者自身が行う。認証設定やDPAPI情報の削除は不要。

- Credential完了前：OAuthログも確認。
- VideosRequestで失敗：HTTP到達前のDNS・TLS・接続・12秒期限等をexception/cause/socketで絞る。
- VideosResponse HTTP4xx/5xx：API側の認証・権限・クォータ等をreasonで確認。
- ActiveLiveChatIdで失敗：配信状態・チャット無効等を確認。
- StreamRequestで失敗：HTTP/2ヘッダー受信前。socket/TLS/HTTPプロトコルと15秒期限を確認。HTTP応答がなければサーバーのgRPC拒否とは断定しない。
- GrpcStatusで失敗：数値コードとreasonを確認。
- Receive/Parseで失敗：接続確立後の通信断・フレーム不正・protobuf不正を区別。

接続成功やVanilla互換性・実YouTubeでの継続受信は模擬テストだけでは保証しない。今回の実機原因は未特定。
