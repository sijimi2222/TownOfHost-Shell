# Shellのゲーム内バグ報告

ロビーの赤いBUGボタンから件名と本文を入力し、「バグを報告する」でDiscordの「バグ報告」フォーラムに新しい投稿を作ります。既存のShell バグ修正Botの認証を中継側に設定すれば、同じBot名義で投稿できます。Botの監視・自動修正機能は現在の方針どおりBot投稿を処理対象から外します。

「試合ログの添付」は初期状態でオフです。送信前にオン・オフ、投稿後に添付あり・なしが表示されます。オンの場合だけ、直近の試合の開始・会議・終了、Shellの警告やエラーのソースファイル名と行番号を添付します。添付にはチャット、部屋コード、フレンドコード、アカウント情報、元の `LogOutput.log` やログ本文を含めません。診断記録はメモリで160行まで保持し、次の試合で入れ替わります。ゲームを再起動すると消えます。

## テスト導入

1. `wrangler.jsonc` に運用者が確認したDiscordサーバーIDを設定します。既存フォーラムIDは `1560956581838069901` です。タグ必須のフォーラムでは実際のタグIDを指定します。中継はDiscordから取得したフォーラムの名前・種類・サーバー・IDを投稿前に確認します。
2. 中継用のHTTPSルートを設定します。Botにフォーラムを表示・投稿できる権限を与え、添付を有効にする場合はファイル添付権限も確認します。トークンはWorkerのSecret `DISCORD_BOT_TOKEN` に設定します。トークンはゲーム設定、DLL、Git、チャットに入れないでください。 [Discordのフォーラム投稿API](https://docs.discord.com/developers/resources/channel#start-thread-in-forum-or-media-channel) · [WorkersのSecret](https://developers.cloudflare.com/workers/configuration/secrets/)
3. Botの資格情報と異なるランダムな受付キーをWorkerのSecret `REPORT_ACCESS_KEY` に設定します。初回は配布範囲を絞った試験用です。キーを公開DLLや公開ZIPに配布せず、試験者のローカル設定にのみ入れます。受付はIP単位と全体でレート制限します。 [Workersのレート制限](https://developers.cloudflare.com/workers/runtime-apis/bindings/rate-limit/)
4. DLL導入後にBUGボタンを一度開くと、BepInExのShell設定ファイルに `[Bug Reports]` が作られます。ゲームを閉じて `EndpointUrl` に `https://<運用ホスト>/v1/bug-reports`、`AccessKey` に試験用受付キーを設定します。
5. 認められた試験フォーラムで、架空の件名・本文を添付オフ／オンの各1件で送信し、一操作につき1スレッド、Bot名義、本文、添付、タグを確認します。結果を確認できない場合は、自動再送せずDiscord上で既存投稿を照合します。

ローカル試験は `node --test worker.test.mjs` で実行します。Discord通信はテスト内の代替処理を使用します。
