# Express：STEP 7実機結果と解除修正

2026-11-15。本人の手動実機試験（Windows TOH-Shellホスト、Pixel 10a通常版、非公開2人部屋）で、参加・Express配役・目視加速・会議後の維持・Express無効の次試合で通常速度へ復元を確認。正確な速度倍率と同一試合での解除は未検証。

## 修正根拠

`RpcSetCustomRole`は旧役職の`RoleBase.Dispose()`→`OnDestroy()`を呼ぶ。旧Expressはこのフックを持たず、加算した速度が残る。減算を置いていた`ChengeRoleAdd()`は生成後のフックであり、解除用ではない。

実Expressと本番RoleBase.Dispose本体をリンクした回帰テストで、旧ソースでは1.0+1.5から解除しても2.5が残り、途中生成通知では2.5が1.0へ戻ることを再現。Unity、オプション、設定送信は代替で、実Androidの速度受理を証明するものではない。

## 変更範囲

- Express.csのみ、減算をOnDestroyへ移動。初期加算と設定ID/値域/既定値を維持。
- 加算量をインスタンスごとに記録。適用済み辞書とplayerIdを保存し、二重適用/二重解除・失効player参照・次試合への遅延減算を防ぐ。
- 解除時に既存の本人用ゲーム設定をdirty化。新規RPC・即時送信は追加しない。
- 他の加算を基礎速度で上書きしない。別役職の乗算/固定速度変更との全組合せは未検証。
- RoleBase.Dispose自体の二重呼出し一般を変更していない。冪等性の保証範囲はExpress.OnDestroyの速度回収。

## 検証手順

`dotnet run --project tests/Express.Runtime.Tests -c Release -p:CopyToAmongUs=false -p:NuGetAudit=false`

52件：生成/途中生成・実Disposeからの解除、全設定値、複数人/インスタンス別設定、二重回収、player参照消失、速度エントリ削除、次試合辞書への影響、本人だけのdirty化。GeneratedDisposal.csはRoleBase.Disposeの本体との一致を検査。

旧ソースの再現にはExpressSourceプロパティをローカル保護コピーへ指定。最終52件の同一テストで修正前6成功/46失敗、修正後52成功/失敗0。独立Androidの純粋計算試験と混同しない。

関連テスト：OptionIds.Regression.Tests 41件、RoleOrigins.Tests 19件。合計112件。全回帰スイートの一括再実行は今回行わない。

ビルド・保全結果はローカルartifacts/step7-express-fixに記録。Steam DLLは配置しない。修正後の同一試合中の解除は、別途配置の許可と本人による実機確認が必要。

Release/ShellDevelopmentとも成功（各0エラー・21警告、Express.cs警告なし）。全ビルドCopyToAmongUs=false。旧ビルドとの警告増減の比較は未実施。
