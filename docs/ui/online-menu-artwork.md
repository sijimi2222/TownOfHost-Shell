# オンラインメニュー用イラストの差し替え

既存のプレイ画面背景とロゴを再利用した仮UIです。オンライン画面の画像枠2枚を独立管理し、画像がなくても操作できます。

## ファイルだけでの差し替え

ゲームを終了し、以下へPNGを置いて起動し直します。ソース変更・当たり判定変更・再ビルドは不要です。

- `BepInEx/config/TOH-Shell/MainMenu/Online/create.png`：左のゲーム作成
- `BepInEx/config/TOH-Shell/MainMenu/Online/join.png`：右のコード入力

PNG1枚16MiB以下、各辺4096px以下。元画像はそのまま保存し、縦横比を維持して中央cover表示します。
読み込みに失敗した画像だけ空白の仮パネルへ戻ります。画像の再読み込みはメニュー生成時です。

## 拡大・切り抜き位置

必要に応じ同じ場所に`create.json` / `join.json`を追加できます。

```json
{"Zoom":1.0,"OffsetX":0.0,"OffsetY":0.0}
```

Zoomは1～3。OffsetX/OffsetYは-1～1で、左/上=-1、中央=0、右/下=1です。
これらは画像の切り抜き位置だけに作用し、クリック範囲とラベル位置は固定です。不正な設定は既定値へ戻ります。

## Unity側の構造

- `SeasideOnlineArtwork`：見出し
- `Seaside_OnlineCreate` / `Seaside_OnlineCode`：固定8頂点PolygonCollider2D付きのクリック領域
- `Motion_OnlineCreate` / `Motion_OnlineCode`：既存のホバー・押し込み演出を適用する見た目の親
- `OnlineCreateArtwork` / `OnlineCodeArtwork`：独立したSpriteRenderer。画像なしではenabled=false
- `OnlineCreateImagePlaceholder` / `OnlineCodeImagePlaceholder`：画像なしでも残る仮パネル

位置・サイズは`Modules/MainMenu/SeasideOnlineLayout.cs`へ集約しています。将来、枠全体のデザインを変える場合は同クラスと表示処理で調整できます。
現在の枠内のイラスト追加はPNG差し替えだけで対応できます。

## 標準処理への接続

標準OpenOnlineMenuの初期化/認証と既存Shellのオンラインボタン設定が完了した後、標準Prefabの
`Create Lobby Button`と`Enter Code Button`のイベントを保持し、既存プレイ画面と同じResetScreenアダプタで表示をShellへ戻します。
カードを押すと、保持した標準OnClickを1回だけ呼びます。作成オプション・コード入力・サーバー選択・接続処理の実装は変更しません。
未知のPrefabや不足するボタンの場合は、新画面を表示せず標準画面へフォールバックします。
戻るとESCはShellホームへ戻り、波ON/OFF設定も既存のものを使います。

## 実機での確認

ゲーム作成/コード入力後の閉じる・ESC・アカウント未ログイン時・接続失敗時の復帰、標準サーバー選択、
マウス/ゲームパッド、解像度変更、画像の色や日本語フォント、実GPUでのリソース解放はゲーム内確認が必要です。
自動テストはUnity/入力/通信の代替実装を使用し、実接続や実ゲームの描画を検証していません。
