# Camouflage・ゲーム終了の回帰テスト

`dotnet run --project tests/Camouflage.EndGame.Tests -c Release -p:CopyToAmongUs=false`

実際の `Modules/Camouflague.cs` をリンクして実行します。`GeneratedFlow.cs` の
`Prefix`・`StartEndGame`・`CoEndGame` は本番ソースから抜き出したメソッドです。
起動時にソースとの SHA-256 一致を検査します。通常インポスター勝利の人数条件も
本番の条件式を取り出していますが、人数の取得元は合成プレイヤーです。

ネイティブ境界・送信・Unityコルーチン・他役職は `Stubs.cs` で代替しています。
不正なネイティブ文字列に相当する PetId 取得例外、色の取得・設定失敗、
送信前・送信後の例外、会議復元、5ラウンド反復、勝利通知までの制御フローを検証します。
ゲーム起動、ネットワーク送信、実プレイヤー・本物の認証情報の利用は行いません。

条件付き勝利のモデルや他役職のスタブは、その役職自体の正しさを保証しません。
AntiBlackout の実通信、IL2CPP のメモリ破損、Vanilla の画面表示も検証対象外です。
これらは実機で別途確認してください。

本番終了フローを変更した場合は、差分をレビューした後に
`python tests/Camouflage.EndGame.Tests/RefreshFixtures.py` を実行し、生成差分を確認します。
ハッシュ失敗を回避する目的だけで更新しないでください。
テスト専用列挙体の値は合成値であり、ゲームの Role ID・RPC ID ではありません。
