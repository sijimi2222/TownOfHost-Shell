# ローカル開発版の検証

この開発ブランチはv2.2.3の公開版タグとは別です。Novaの射程50、壁・扉貫通、TMP描画、通信分散、外見同期と後片付け、マップ別の表示プール、Shell Debug Controllerを含みます。The Fungleは照準の星を含め11体、その他は10体です。

**Novaの実機検証は未完了です。** 静的検査や代替メッシュの単体テストは、Vanilla PC/Switch/スマホでの表示成功や一斉切断の解消を証明しません。実測通信と15人モデルの推定は別です。非公開の小規模テストから検証してください。

## ビルド

Windows、.NET 6 SDK対応ターゲットと.NET 10 SDK／Desktop Runtime、正当にインストールしたAmong UsとBepInExの参照DLLが必要です。参照DLLやゲーム本体は含みません。TownOfHost.csprojのAmongUsプロパティは必要ならコマンド引数で指定してください。

```powershell
dotnet build TownOfHost.csproj -c Release -p:CopyToAmongUs=false
dotnet build TownOfHost.csproj -c ShellDevelopment -p:CopyToAmongUs=false
.\tools\ShellDebugController\Build-Controller.ps1
```

Releaseには新しいデバッグIPC・壁すり抜け・生存ホストズームを含めません。ShellDevelopmentは専用シンボルとフラグが必要で、自動ゲーム配置は禁止です。Build-Controller.ps1は両版のアセンブリを監査し、日時付きartifactsフォルダへ保存します。ゲーム・アプリを起動しません。

## 回帰テスト

```powershell
$env:TOHS_VERSION_SOURCE = (Get-Location).Path
$env:TOHS_LIVE_PROBE = '0'
Get-ChildItem tests -Recurse -Filter *.csproj | ForEach-Object {
    dotnet run --project $_.FullName -c Release -p:CopyToAmongUs=false -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { throw "Test failed: $($_.FullName)" }
}
```

ゲーム型・ネットワークはテスト用代替を使います。Streaming43505.TestsはlocalhostだけのHTTP試験です。実WindowsのローカルNamed Pipeを追加検証する場合はDebugController.TestsとDebugController.Net6.Testsへ`-- --windows-pipe`を渡します。Controllerの`--ui-smoke <出力PNG>`は合成データだけの自己テストで、ゲームIPCを開始しません。

テストに個人の設定・ログ・バックアップは不要です。数値設定の保存試験はメモリ上で生成した合成データを使います。ビルド生成物・ログ・アプリ配布物はGitへ追加しないでください。
