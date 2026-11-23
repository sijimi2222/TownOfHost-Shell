// Generated from reviewed production methods. Regenerate manually; hashes are checked at runtime.
using System.Text.RegularExpressions;
using static TownOfHost.Translator;
namespace TownOfHost {
public static class UtilsRoleInfo {
        private static string FixRoleNameInput(string text)

        {

            return text.RemoveHtmlTags() switch

            {

                "GM" or "gm" or "ゲームマスター" => GetString("GM"),



                //インポスター

                "イビルゲッサー" or "いびるげっさー" or "いびげ" or "イビゲ" => GetString("EvilGuesser"),

                "イビルムービング" or "いびるむーびんぐ" => GetString("EvilMoving"),

                "ミニマリスト" or "みにまりすと" => GetString("Minimalist"),

                "イビルリンカー" or "いびるりんかー" => GetString("EvilLinker"),

                "自爆魔" or "じばくま" or "ジバクマ" => GetString("SelfBomber"),

                "スウーパー" or "すうーぱー" => GetString("Super"),

                "イビルスタンドマスター" or "いびるすたんどますたー" => GetString("EvilStandMaster"),

                "波動砲" or "はどうほう" => GetString("WaveCannon"),

                "ダブルキラー" or "だぶるきらー" or "だぶきら" or "ダブキラ" => GetString("DoubleKiller"),

                "侍" or "さむらい" or "サムライ" => GetString("Samurai"),

                "テレポーター" or "てれぽーたー" or "テレポ" or "てれぽ" => GetString("Teleporter"),

                "エボルバー" or "えぼるばー" or "エボ" or "えぼ" => GetString("Evolver"),

                "ビギナーインポスター" or "びぎなーいんぽすたー" or "びぎいん" or "ビギイン" => GetString("BeginnerImpostor"),

                "マスマーダー" or "ますまーだー" or "ますま" or "マスマ" => GetString("MassMurderer"),

                "タイムリーパー" or "たいむりーぱー" or "タイリ" or "たいり" => GetString("TimeReaper"),

                "チェイサー" or "ちぇいさー" => GetString("Chaser"),

                "エイリアン" or "えいりあん" => GetString("Alien"),

                "ハイジャックエイリアン" or "はいじゃっくえいりあん" => GetString("HijackAlien"),

                "ジャンパー" or "じゃんぱー" => GetString("Jumper"),

                "イビルハッカー" or "いびるはっかー" or "イビハカ" or "いびはか" => GetString("EvilHacker"),

                "イビルトラッカー" or "いびるとらっかー" or "イビトラ" or "いびとら" => GetString("EvilTracker"),

                "イビルギャンブラー" or "いびるぎゃんぶらー" or "イビギャン" or "いびぎゃん" => GetString("EvilGambler"),

                "イビルサテライト" or "いびるさてらいと" or "イビサテ" or "いびさて" => GetString("EvilSatellite"),

                "イビルアドナー" or "いびるあどなー" or "イビアド" or "いびあど" => GetString("EvilAddoner"),

                "イビルメーカー" or "いびるめーかー" or "イビメ" or "いびめ" => GetString("EvilMaker"),

                "イビルテラー" or "いびるてらー" or "イビテラ" or "いびてら" => GetString("EvilTeller"),

                "イビルブレンダー" or "イビルブレンダー" => GetString("EvilBlender"),

                "バルーナー" or "ばるーなー" => GetString("Baluner"),

                "爆弾魔" or "ボマー" or "ばくだんま" or "ぼまー" => GetString("Bomber"),

                "マジシャン" or "まじしゃん" => GetString("Magician"),

                "プロボーラー" or "ぷろぼーらー" => GetString("ProBowler"),

                "テレポートキラー" or "てれぽーときらー" or "テレキラ" or "てれきら" => GetString("TeleportKiller"),

                "花火職人" or "はなびしょくにん" or "はなびしょくじん" or "はなび" or "花火" => GetString("FireworksExpert"),

                "コネクトセーバー" or "こねくとせーばー" or "コネセ" or "こねせ" => GetString("ConnectSaver"),

                "ウィッチ" or "うぃっち" or "まじょ" or "マジョ" or "魔女" => GetString("Witch"),

                "スナイパー" or "すないぱー" or "すな" or "スナ" or "砂" => GetString("Sniper"),

                "アーチャー" or "あーちゃー" => GetString("Archer"),

                "ペンギン" or "ぺんぎん" => GetString("Penguin"),

                "タイムシーフ" or "たいむしーふ" => GetString("TimeThief"),

                "ヴァンパイア" or "ヴぁんぱいあ" or "バンパイア" or "ばんぱいあ" or "ヴァンプ" or "ヴぁんぷ" or "ばんぷ" => GetString("Vampire"),

                "パペッティア" or "ぱぺってぃあ" or "ぱぺ" or "パペ" => GetString("Puppeteer"),

                "ウォーロック" or "うぉーろっく" or "ウォロ" or "うぉろ" => GetString("Warlock"),

                "メアー" or "めあー" or "だーくきらー" or "ダークキラー" => GetString("Mare"),

                "プログレスキラー" or "ぷろぐれすきらー" or "ぷろきら" or "プロキラ" => GetString("ProgressKiller"),

                "インサイダー" or "いんさいだー" or "インサイ" or "いんさい" => GetString("Insider"),

                "モグラ" or "もぐら" => GetString("Mole"),

                "アンチレポーター " or "あんちれぽーたー" or "アンレポ" or "あんれぽ" => GetString("AntiReporter"),

                "イレイサー" or "いれいさー" => GetString("Eraser"),

                "シェイプマスター" or "しぇいぷますたー" or "シェイマス" or "しぇいます" => GetString("ShapeMaster"),

                "シェイプキラー" or "しぇいぷきらー" or "シェイキラ" or "しぇいきら" => GetString("ShapeKiller"),

                "ステルス" or "すてるす" => GetString("Stealth"),

                "ネコカボチャ" or "ねこかぼちゃ" or "ネコカボ" or "ねこかぼ" or "猫カボチャ" or "猫かぼちゃ" or "猫かぼ" or "猫カボ" => GetString("CatPumpkin"),

                "マフィア" or "まふぃあ" => GetString("Mafia"),

                "カリスマスター" or "かりすますたー" or "カリスマ" or "かりすま" => GetString("CharismaMaster"),

                "大狼" or "たいろう" or "大老" or "おおろう" => GetString("Tairou"),//適当に入れたけどおおろうって何?

                "アンフォーチュナー" or "あんふぉーちゅなー" or "案フォーチュナー" or "アンフォ" or "あんふぉ" or "案フォ" or "案ふぉ" => GetString("Unfortuner"),

                "シリアルキラー" or "しりあるきらー" or "シリキラ" or "しりきら" or "シリアル" or "しりある" => GetString("SerialKiller"),//ちなみに俺は呼び方シリアルが好き

                "バウンティハンター" or "ばうんてぃはんたー" or "バウンティ" or "ばうんてぃ" or "ばうはん" or "バウハン" => GetString("BountyHunter"),

                "カーサー" or "かーさー" or "カーサ" or "かーさ" => GetString("Curser"),

                "リミッター" or "りみったー" or "リミッタ" or "りみった" or "りみた" or "リミタ" => GetString("Limiter"),//りみたは今誤字ったから！

                "リローダー" or "りろーだー" => GetString("Reloader"),

                "クイックキラー" or "くいっくきらー" or "クイキラ" or "くいきら" => GetString("QuickKiller"),

                "ノーティファー" or "のーてぃふぁー" or "ノーティ" or "のーてぃ" => GetString("Notifier"),

                "記憶喪失者" or "きおくそうしつしゃ" or "記憶喪失" or "きおくそうしつ" or "偽シェリフ" or "にせシェリフ" or "にせしぇりふ" or "偽狼少年" or "にせ狼少年" => GetString("Amnesia"),

                "ボーダーキラー" or "ぼーだーきらー" or "ボダキラ" or "ぼだきら" => GetString("BorderKiller"),

                "デクレッシェンド" or "でくれっしぇんど" or "デクレ" or "でくれ" => GetString("Decrescendo"),

                "プロボウラー" or "プロボーラー" => GetString("ProBowler"),

                "一途な狼" or "一途" or "いちず" => GetString("EarnestWolf"),

                "インポスター" or "いんぽすたー" or "いんぽ" or "インポ" or "いんぽす" or "インポス" or "imp" => GetString("Impostor"),

                "シェイプシフター" or "しぇいぷしふたー" or "しぇいぷしふ" or "シェイプシフ" or "しぇいぷ" or "シェイプ" => GetString("Shapeshifter"),

                "ファントム" or "ふぁんとむ" or "亡霊" or "ぼうれい" or "ボウレイ" => GetString("Phantom"),

                "ヴァイパー" or "バイパー" => GetString("Viper"),



                //マッドメイト

                "マッドメイト" or "まっどめいと" or "マッド" or "まっど" or "まどめ" or "マドメ" => GetString("Madmate"),

                "マッドスニッチ" or "まっどすにっち" => GetString("MadSnitch"),

                "マッドガーディアン" or "まっどがーでぃあん" => GetString("MadGuardian"),

                "マッドスーサイド" or "まっどすーさいど" or "崇拝者" or "スーサイド" => GetString("MadSuicide"),

                "マッドトラッカー" or "まっどとらっかー" => GetString("MadTracker"),

                "マッドチェンジャー" or "まっどちぇんじゃー" => GetString("MadChanger"),

                "マッドベイト" or "まっどべいと" => GetString("MadBait"),

                "マッドテラー" or "まっどてらー" => GetString("MadTeller"),

                "マッドハッカー" or "まっどはっかー" => GetString("MadHacker"),

                "マッドリデュース" or "まっどりでゅーす" => GetString("MadReduce"),

                "マッドアベンジャー" or "まっどあべんじゃー" => GetString("MadAvenger"),

                "マッドジェスター" or "まっどじぇすたー" => GetString("MadJester"),

                "マッドべトレイヤー" or "まっどべとれいやー" or "マッドベとレイヤー" => GetString("MadBetrayer"),

                "マッドワーカー" or "まっどわーかー" => GetString("MadWorker"),

                "マッドシェリフ" or "まっどしぇりふ" => GetString("MadSheriff"),

                "黒猫" or "くろねこ" or "クロネコ" => GetString("BlackCat"),

                "サイドキックマッドメイト" or "さいどきっくまっどめいと" or "skまっど" or "SKまっど" or "SKマッド" or "SKまっど" => GetString("SKMadmate"),



                //クルーメイト

                "ナイスイレイサー" or "ないすいれいさー" => GetString("NiceEraser"),

                "ナイスゲッサー" or "ないすげっさー" or "ないげ" or "ナイゲ" => GetString("NiceGuesser"),

                "ナイス猫又" or "ないすねこまた" or "ないねこ" or "ナイネコ" => GetString("NiceNekomata"),

                "ナイスリンカー" or "ないすりんかー" or "ないりん" or "ナイリン" => GetString("NiceLinker"),

                "ナイストラッパー" or "ないすとらっぱー" or "ないとら" or "ナイトラ" => GetString("NiceTrapper"),

                "ナイステレポーター" or "ないすてれぽーたー" or "ないてれ" or "ナイテレ" => GetString("NiceTeleporter"),

                "魔法少女" or "まほうしょうじょ" or "マホウショウジョ" or "まほしょう" or "マホショウ" => GetString("MagicalGirl"),

                "村長" or "そんちょう" or "ソンチョウ" or "むらちょう" or "ムラチョウ" => GetString("VillageChief"),

                "波動砲シェリフ" or "はどうほうしぇりふ" => GetString("WaveCannonSheriff"),

                "あやしい占い師" or "怪しい占い師" or "あやしいうらないし" or "アヤシイウラナイシ" => GetString("SuspiciousTeller"),

                "かけだし占い師" or "駆け出し占い師" or "かけだしうらないし" or "カケダシウラナイシ" => GetString("AmateurTeller"),

                "ぽんこつ占い師" or "ポンコツ占い師" => GetString("PonkotuTeller"),

                "霊媒師" or "れいばいし" or "レイバイシ" => GetString("Medium"),

                "ジェイラー" or "じぇいらー" => GetString("Jailer"),

                "ニムロッド" or "にむろっど" or "にむろつど" or "ニムロツド" => GetString("Nimrod"),

                "ワルキューレ" or "わるきゅーれ" or "わるきゆーれ" or "ワルキユーレ" => GetString("Walkure"),

                "牛乳屋" or "ぎゅうにゅうや" or "ギュウニュウヤ" => GetString("Milkman"),

                "賢者" or "けんじゃ" or "ケンジャ" => GetString("Sage"),

                "子方" or "こかた" or "コカタ" or "しかた" or "シカタ" => GetString("Apprentice"),

                "ラビット" or "らびっと" or "うさぎ" or "ウサギ" or "兎" or "兔" => GetString("Rabbit"),

                "サンタ" or "さんた" or "ひげのおじさん" or "メリークリスマス！！" => GetString("Santa"),

                "ムービング" or "むーびんぐ" => GetString("Moving"),

                "プクプク" or "ぷくぷく" or "ﾌﾟｸﾌﾟｸ" => GetString("Pukupuku"),

                "ヒッチハイカー" or "ひっちはいかー" or "ストーカー" => GetString("Hitchhiker"),

                "オールラウンダー" or "おーるだうんだー" or "オルラ" or "おるら" => GetString("Allrounder"),

                "ナイスアドナー" or "ナイアド" or "ないあど" => GetString("NiceAddoer"),

                "ナイスロガー" or "ないすろがー" => GetString("NiceLogger"),

                "フォーチュナー" or "ふぉーちゅなー" or "ふぉーつなー" => GetString("Fortuner"),

                "シェリフ" or "しぇりふ" or "猿" or "さる" => GetString("Sheriff"),

                "ミーティングシェリフ" or "みーてぃんぐしぇりふ" => GetString("MeetingSheriff"),

                "メイヤー" or "めいやー" => GetString("Mayor"),

                "巫女" or "みこ" or "ミコ" or "ふじょ" or "フジョ" => GetString("ShrineMaiden"),

                "捜査官" or "そうさかん" or "ソウサカン" => GetString("Inspector"),

                "ディクテーター" or "でぃくてーたー" => GetString("Dictator"),

                "ホワイトハッカー" or "ほわいとはっかー" or "ほわは" or "ホワハ" => GetString("WhiteHacker"),

                "天秤" or "てんびん" or "テンビン" => GetString("Balancer"),

                "観察者" or "かんさつしゃ" or "カンサツシャ" => GetString("Observer"),

                "アナライザー" or "あならいざー" => GetString("Analyzer"),

                "ウルトラスター" or "うるとらすたー" or "ひかるごみ" or "光るゴミ" or "スター" => GetString("UltraStar"),

                "タスクスター" or "たすくすたー" => GetString("TaskStar"),

                "パン屋" or "ぱんや" or "パンヤ" or "ベーカリー" => GetString("Bakery"),

                "エクスプレス" or "えくすぷれす" or "しんかんせん" or "新幹線" or "シンカンセン" => GetString("Express"),

                "ストルナー" or "すとるなー" or "属性サンタ" => GetString("Stolener"),

                "ベイト" or "べいと" => GetString("Bait"),

                "インセンダー" or "いんせんだー" => GetString("InSender"),

                "ガードマスター" or "がーどますたー" or "しーるだー" or "シールダー" => GetString("GuardMaster"),

                "君臨者" or "くんりんしゃ" or "クンリンシャ" or "きんぐ" or "キング" => GetString("King"),

                "トラッパー" or "とらっぱー" => GetString("Trapper"),

                "スニッチ" or "すにっち" => GetString("Snitch"),

                "ドクター" or "どくたー" or "いしゃ" or "医者" or "イシャ" => GetString("Doctor"),

                "シーア" or "しーあ" => GetString("Seer"),

                "サイキック" or "さいきっく" => GetString("Psychic"),

                "サテライト" or "さてらいと" => GetString("Satelite"),

                "ライター" or "らいたー" => GetString("Lighter"),

                "スピードブースター" or "すぴーどぶーすたー" => GetString("SpeedBooster"),

                "タイムマネージャー" or "たいむまねーじゃー" => GetString("TimeManager"),

                "エフィシェンシー" or "えふぃしぇんしー" or "えふぃふぇんしー" or "エフィフェンシー" or "えふぃふぇん" or "えふぃしぇん" or "エフェフェン" or "エフィシェン" => GetString("Efficient"),

                "サボタージュマスター" or "さぼたーじゅますたー" or "さぼます" or "サボマス" => GetString("SabotageMaster"),

                "キャンドルライター" or "きゃんどるらいたー" or "きゃんらい" or "キャンライ" => GetString("キャンライ"),

                "雪だるま" or "ゆきだるま" or "ユキダルマ" => GetString("Snowman"),

                "ウォーカー" or "うぉーかー" => GetString("Walker"),

                "カムバッカー" or "かむばっかー" or "かむば" or "カムバ" or "噛むバッカー" => GetString("ComeBacker"),

                "アンドロイド" or "あんどろいど" or "すまほ" or "android" => GetString("Android"),

                "スタッフ" or "すたっふ" => GetString("Staff"),

                "トイレファン" or "といれふぁん" or "といれ" or "トイレ" => GetString("ToiletFan"),

                "ベントマスター" or "べんとますたー" => GetString("VentMaster"),

                "ベントオープナー" or "べんとおーぷなー" => GetString("VentOpener"),

                "ベントハンター" or "べんとはんたー" => GetString("VentHunter"),

                "エンジニア" or "えんじにあ" => GetString("Engineer"),

                "科学者" or "かがくしゃ" => GetString("Scientist"),

                "トラッカー" or "とらっかー" => GetString("Tracker"),

                "ノイズメーカー" or "のいずめーかー" => GetString("Noisemaker"),

                "裁判官" or "ジャッジ" => GetString("Judge"),

                "クルー" or "クルーメイト" or "くるーめいと" or "くるー" => GetString("Crewmate"),

                "狼少年" or "オオカミ少年" or "おおかみ少年" or "おおかみしょうねん" or "オオカミショウネン" => GetString("WolfBoy"),

                "ギャスプ" or "ギャプス" or "ギャスフ" or "ぎゃすぷ" => GetString("Gasp"),//これを許していいのか。

                "さつまといも" or "さつもといも" or "satsumatoimo" or "satsumotoimo" => GetString("SatsumatoImo"),

                "さつまといもc" or "さつもといもc" or "satsumatoimoc" or "satsumotoimoc" => GetString("SatsumatoImoC"),

                "さつまといもm" or "さつもといもm" or "satsumatoimom" or "satsumotoimom" => GetString("SatsumatoImoM"),

                //第3陣営

                "独裁者" or "どくさいしゃ" or "ドクサイシャ" or "dokusaisha" => GetString("Autocrat"),

                "アーソニスト" or "あーそにすと" or "asonisuto" => GetString("Arsonist"),

                "ジェスター" or "じぇすたー" or "jesuta" => GetString("Jester"),

                "テロリスト" or "てろりすと" or "terorisuto" => GetString("Terrorist"),

                "エクスキューショナー" or "えくすきゅーしょなー" or "ekusukyushona" => GetString("Executioner"),

                "シュレディンガーの猫" or "しゅれでぃんがーのねこ" or "シュレ猫" or "しゅれねこ" or "shuredinganoneko" or "shureneko" => GetString("SchrodingerCat"),

                "オポチュニスト" or "おぽちゅにすと" or "opochunisuto" => GetString("Opportunist"),

                "フリーター" or "ふりーたー" or "furita" => GetString("Freeter"),

                "神" or "かみ" or "カミ" or "kami" => GetString("God"),

                "マグロ" or "まぐろ" or "maguro" => GetString("Tuna"),

                "陰陽師" or "おんみょうじ" or "オンミョウジ" or "onmyoji" => GetString("Onmyoji"),

                "式神" or "しきがみ" or "シキガミ" or "shikigami" => GetString("Shikigami"),

                "ゾンビ" or "ぞんび" or "zonbi" => GetString("Zombie"),

                "エゴイスト" or "えごいすと" or "egoisuto" => GetString("Egoist"),

                "ジャッカル" or "じゃっかる" or "jakkaru" or "じゃっこー" or "jk" => GetString("Jackal"),

                "波動砲ジャッカル" or "はどうほうじゃっかる" or "ハドウホウジャッカル" or "hadouhoujakkaru" => GetString("JackalHadouHo"),

                "弾" or "たま" or "タマ" or "tama" => GetString("Tama"),

                "ジャッカルエイリアン" or "じゃっかるえいりあん" or "jakkarueirian" => GetString("JackalAlien"),

                "ジャッカルウルフ" or "じゃっかるうるふ" or "jakkaruurufu" => GetString("JackalWolf"),

                "ジャッカルマフィア" or "じゃっかるまふぃあ" or "jakkarumafia" => GetString("JackalMafia"),

                "ジャッカルドール" or "じゃっかるどーる" or "jakkarudoru" or "さいどきっく" or "サイドキック" or "どーる" or "ドール" => GetString("Jackaldoll"),

                "ペスト医師" or "ぺすといし" or "ペストイシ" or "pesutoishi" => GetString("PlagueDoctor"),

                "リモートキラー" or "りもーときらー" or "rimotokira" => GetString("Remotekiller"),

                "シェフ" or "しぇふ" or "shefu" => GetString("Chef"),

                "カウントキラー" or "かうんときらー" or "kauntokira" or "かうきら" or "カウキラ" => GetString("CountKiller"),

                "死神" or "しにがみ" or "シニガミ" or "shinigami" => GetString("GrimReaper"),

                "マドンナ" or "まどんな" or "madonna" => GetString("Madonna"),

                "ワーカホリック" or "わーかほりっく" or "wakahorikku" => GetString("Workaholic"),

                "モノクラー" or "ものくらー" or "monokura" => GetString("Monochromer"),

                "ドッペルゲンガー" or "どっぺるげんがー" or "dopperugenga" or "どっぺる" or "ドッペル" => GetString("DoppelGanger"),

                "マスメディア" or "ますめでぃあ" or "masumedia" => GetString("MassMedia"),

                "カメレオン" or "かめれおん" or "kamereon" => GetString("Chameleon"),

                "バンカー" or "ばんかー" or "banka" => GetString("Banker"),

                "バケネコ" or "ばけねこ" or "bakeneko" => GetString("BakeCat"),

                "怪盗" or "かいとう" or "カイトウ" or "kaito" => GetString("PhantomThief"),

                "カースメーカー" or "かーすめーかー" or "kasumeka" => GetString("CurseMaker"),

                "妖狐" or "ようこ" or "ヨウコ" or "yoko" or "きつね" or "キツネ" or "kitsune" => GetString("Fox"),

                "サンタクロース" or "さんたくろーす" or "santakurosu" => GetString("SantaClaus"),

                "ヴァルチャー" or "ゔぁるちゃー" or "varucha" or "バルチャー" or "ばるちゃー" or "barucha" => GetString("Vulture"),

                "裏切者" or "うらぎりもの" or "ウラギリモノ" or "uragirimono" => GetString("Turncoat"),

                "藁人形" or "わらにんぎょう" or "ワラニンギョウ" or "waraningyo" => GetString("Strawdoll"),

                "ミッショナー" or "みっしょなー" or "misshona" => GetString("Missioneer"),

                "愚か者" or "おろかもの" or "オロカモノ" or "orokamono" => GetString("Fool"),

                "キューピッド" or "きゅーぴっど" or "kyupiddo" => GetString("Cupid"),

                "イーター" or "いーたー" or "ita" => GetString("Eater"),

                "スペランカー" or "すぺらんかー" or "superanka" => GetString("Spelunker"),

                "忘却者" or "ぼうきゃくしゃ" or "ボウキャクシャ" or "bokyakusha" => GetString("Oblivion"),

                "パブロフの犬" or "ぱぶろふのいぬ" or "paburofunoinu" => GetString("PavlovDog"),

                "パブロフのオーナー" or "ぱぶろふのおーなー" or "paburofunoonaa" => GetString("PavlovOwner"),

                "ぱぶろふ" or "パブロフ" or "paburofu" => GetString("PavlovDogImprint"),

                "モイラ" or "もいら" or "moira" => GetString("Moira"),

                "毒入りパン屋" or "どくいりぱんや" or "ドクイリパンヤ" or "dokuiripanya" => GetString("PoisonedBakery"),

                "チャッター" or "ちゃったー" or "chatta" => GetString("Chatter"),

                "爆ぜ師" or "はぜし" or "ハゼシ" or "hazeshi" => GetString("LoversBreaker"),

                "自殺願望者" or "じさつがんぼうしゃ" or "ジサツガンボウシャ" or "jisatsuganbosha" => GetString("Suicider"),

                "スタンドマスター" or "すたんどますたー" or "sutandomasuta" => GetString("StandMaster"),

                "スタンド" or "すたんど" or "sutando" => GetString("Stand"),

                "シャイボーイ" or "しゃいぼーい" or "shaiboi" => GetString("Shyboy"),

                "ヴィラン" or "ゔぃらん" or "viran" => GetString("Villain"),

                "スクラッチャー" or "すくらっちゃー" or "sukuraccha" => GetString("Scratcher"),

                "モニカ(未完成)" or "もにか" or "モニカ" or "monikamikansei" or "モニカ" or "もにか" or "monika" => GetString("Monika"),

                "鬼" or "おに" or "オニ" or "oni" => GetString("Ogre"),

                "弁護士" or "べんごし" or "ベンゴシ" or "bengoshi" => GetString("Lawyer"),

                "追跡者" or "ついせきしゃ" or "ツイセキシャ" or "tsuisekisha" => GetString("Pursuer"),

                "決闘者" or "けっとうしゃ" or "ケットウシャ" or "kettosha" => GetString("Duelist"),

                "海賊" or "かいぞく" or "カイゾク" or "kaizoku" => GetString("Pirate"),

                "一味" or "いちみ" or "イチミ" or "ichimi" => GetString("Gang"),

                "ポーカーフェイス" or "ぽーかーふぇいす" or "pokafeisu" => GetString("PokerFace"),

                "一番目の仔豚" or "いちばんめのこぶた" or "イチバンメノコブタ" or "ichibanmenokobuta" => GetString("TheFirstLittlePig"),

                "二番目の仔豚" or "にばんめのこぶた" or "ニバンメノコブタ" or "nibanmenokobuta" => GetString("TheSecondLittlePig"),

                "三番目の仔豚" or "さんばんめのこぶた" or "サンバンメノコブタ" or "sanbanmenokobuta" => GetString("TheThirdLittlePig"),

                "ハッピージェスター" or "はっぴーじぇすたー" or "happijesuta" => GetString("HappyJester"),

                "バットガール" or "ばっとがーる" or "battogaru" => GetString("BatGirl"),

                "アマテラス" or "あまてらす" or "amaterasu" => GetString("Amateras"),

                "ルーラー" or "るーらー" or "ru-ra-" or "ruler" => GetString("Ruler"),

                "被虐者" or "ひぎゃくしゃ" or "ヒギャクシャ" or "higyakusha" => GetString("Victim"),

                //幽霊役職

                //コンビネーション

                "ラバーズ" or "らばーず" or "rabazu" or "リア充" or "恋人" => GetString("Lovers"),

                "レッドラバーズ" or "れっどらばーず" or "reddorabazu" => GetString("RedLovers"),

                "イエローラバーズ" or "いえろーらばーず" or "ierorabazu" => GetString("YellowLovers"),

                "ブルーラバーズ" or "ぶるーらばーず" or "bururabazu" => GetString("BlueLovers"),

                "グリーンラバーズ" or "ぐりーんらばーず" or "gurinrabazu" => GetString("GreenLovers"),

                "ホワイトラバーズ" or "ほわいとらばーず" or "howaitorabazu" => GetString("WhiteLovers"),

                "パープルラバーズ" or "ぱーぷるらばーず" or "papururabazu" => GetString("PurpleLovers"),

                "マドンナラバーズ" or "まどんならばーず" or "madonnarabazu" => GetString("MadonnaLovers"),

                "キューピッドラバーズ" or "きゅーぴっどらばーず" or "kyupiddorabazu" => GetString("CupidLovers"),

                "片思い" or "かたおもい" or "カタオモイ" or "kataomoi" or "片想い" => GetString("OneLove"),

                "天邪鬼" or "あまのじゃく" or "アマノジャク" or "amanojaku" => GetString("Amanojaku"),

                "徒党" or "ととう" or "トトウ" or "toto" => GetString("Faction"),

                "ドライバーとブレイド" => GetString("Driver"),



                "織姫と彦星" or "おりひめとひこぼし" or "オリヒメトヒコボシ" or "orihimetohikoboshi" => GetString("Vega"),

                _ => text,

            };

        }

        static readonly Dictionary<string, string> KanaRomajiMap = new()

        {

            ["あ"] = "a",

            ["い"] = "i",

            ["う"] = "u",

            ["え"] = "e",

            ["お"] = "o",

            ["か"] = "ka",

            ["き"] = "ki",

            ["く"] = "ku",

            ["け"] = "ke",

            ["こ"] = "ko",

            ["さ"] = "sa",

            ["し"] = "shi",

            ["す"] = "su",

            ["せ"] = "se",

            ["そ"] = "so",

            ["た"] = "ta",

            ["ち"] = "chi",

            ["つ"] = "tsu",

            ["て"] = "te",

            ["と"] = "to",

            ["な"] = "na",

            ["に"] = "ni",

            ["ぬ"] = "nu",

            ["ね"] = "ne",

            ["の"] = "no",

            ["は"] = "ha",

            ["ひ"] = "hi",

            ["ふ"] = "fu",

            ["へ"] = "he",

            ["ほ"] = "ho",

            ["ま"] = "ma",

            ["み"] = "mi",

            ["む"] = "mu",

            ["め"] = "me",

            ["も"] = "mo",

            ["や"] = "ya",

            ["ゆ"] = "yu",

            ["よ"] = "yo",

            ["ら"] = "ra",

            ["り"] = "ri",

            ["る"] = "ru",

            ["れ"] = "re",

            ["ろ"] = "ro",

            ["わ"] = "wa",

            ["を"] = "wo",

            ["ん"] = "n",

            ["が"] = "ga",

            ["ぎ"] = "gi",

            ["ぐ"] = "gu",

            ["げ"] = "ge",

            ["ご"] = "go",

            ["ざ"] = "za",

            ["じ"] = "ji",

            ["ず"] = "zu",

            ["ぜ"] = "ze",

            ["ぞ"] = "zo",

            ["だ"] = "da",

            ["ぢ"] = "ji",

            ["づ"] = "zu",

            ["で"] = "de",

            ["ど"] = "do",

            ["ば"] = "ba",

            ["び"] = "bi",

            ["ぶ"] = "bu",

            ["べ"] = "be",

            ["ぼ"] = "bo",

            ["ぱ"] = "pa",

            ["ぴ"] = "pi",

            ["ぷ"] = "pu",

            ["ぺ"] = "pe",

            ["ぽ"] = "po",

            ["ぁ"] = "a",

            ["ぃ"] = "i",

            ["ぅ"] = "u",

            ["ぇ"] = "e",

            ["ぉ"] = "o",

            ["ゃ"] = "ya",

            ["ゅ"] = "yu",

            ["ょ"] = "yo",

            ["きゃ"] = "kya",

            ["きゅ"] = "kyu",

            ["きょ"] = "kyo",

            ["しゃ"] = "sha",

            ["しゅ"] = "shu",

            ["しょ"] = "sho",

            ["ちゃ"] = "cha",

            ["ちゅ"] = "chu",

            ["ちょ"] = "cho",

            ["にゃ"] = "nya",

            ["にゅ"] = "nyu",

            ["にょ"] = "nyo",

            ["ひゃ"] = "hya",

            ["ひゅ"] = "hyu",

            ["ひょ"] = "hyo",

            ["みゃ"] = "mya",

            ["みゅ"] = "myu",

            ["みょ"] = "myo",

            ["りゃ"] = "rya",

            ["りゅ"] = "ryu",

            ["りょ"] = "ryo",

            ["ぎゃ"] = "gya",

            ["ぎゅ"] = "gyu",

            ["ぎょ"] = "gyo",

            ["じゃ"] = "ja",

            ["じゅ"] = "ju",

            ["じょ"] = "jo",

            ["びゃ"] = "bya",

            ["びゅ"] = "byu",

            ["びょ"] = "byo",

            ["ぴゃ"] = "pya",

            ["ぴゅ"] = "pyu",

            ["ぴょ"] = "pyo",

            ["ふぁ"] = "fa",

            ["ふぃ"] = "fi",

            ["ふぇ"] = "fe",

            ["ふぉ"] = "fo",

            ["ゔぁ"] = "va",

            ["ゔぃ"] = "vi",

            ["ゔ"] = "vu",

            ["ゔぇ"] = "ve",

            ["ゔぉ"] = "vo",

            ["ヴぁ"] = "va",

            ["ヴぃ"] = "vi",

            ["ヴ"] = "vu",

            ["ヴぇ"] = "ve",

            ["ヴぉ"] = "vo",

        };



        static string NormalizeRomajiInput(string text)

            => Regex.Replace(KanaToRomaji(text ?? ""), @"[^a-z0-9]", string.Empty).ToLowerInvariant();



        static string KanaToRomaji(string text)

        {

            var result = "";

            var doubleNext = false;

            for (var i = 0; i < text.Length; i++)

            {

                var current = NormalizeKana(text[i]);

                if (current == 'っ')

                {

                    doubleNext = true;

                    continue;

                }

                if (current == 'ー')

                {

                    var vowel = result.LastOrDefault(ch => "aeiou".Contains(ch));

                    if (vowel != default) result += vowel;

                    continue;

                }



                var key = current.ToString();

                if (i + 1 < text.Length)

                {

                    var next = NormalizeKana(text[i + 1]);

                    var combined = $"{current}{next}";

                    if (KanaRomajiMap.TryGetValue(combined, out var combinedRomaji))

                    {

                        result += doubleNext && combinedRomaji.Length > 0 ? $"{combinedRomaji[0]}{combinedRomaji}" : combinedRomaji;

                        doubleNext = false;

                        i++;

                        continue;

                    }

                }



                if (KanaRomajiMap.TryGetValue(key, out var romaji))

                {

                    result += doubleNext && romaji.Length > 0 ? $"{romaji[0]}{romaji}" : romaji;

                    doubleNext = false;

                }

                else if (char.IsLetterOrDigit(current))

                {

                    result += char.ToLowerInvariant(current);

                    doubleNext = false;

                }

            }

            return result;

        }



        static char NormalizeKana(char c)

            => c >= 'ァ' && c <= 'ヶ' ? (char)(c - 0x60) : c;



        public static bool GetRoleByInputName(string input, out CustomRoles output, bool includeVanilla = false)

        {

            output = new();

            input = Regex.Replace(input, @"[0-9]+", string.Empty);

            input = Regex.Replace(input, @"\s", string.Empty);

            input = Regex.Replace(input, @"[\x01-\x1F,\x7F]", string.Empty);

            input = input.ToLower().Trim().Replace("是", string.Empty);

            if (input == "" || input == string.Empty) return false;

            // 漢字はローマ字にできない(消える)ので、漢字を含む名前をローマ字で比べると
            // 「一途な狼」と「孤独な狼」がどちらも "na" になって、違う役職が出ていた。漢字を含む時はローマ字で比べない
            var hasKanji = input.Any(ch => ch >= '一' && ch <= '鿿');
            var romajiInput = hasKanji ? "" : NormalizeRomajiInput(input);

            input = FixRoleNameInput(input).ToLower();

            // まず名前がぴったり同じ役職を探す(ローマ字の一致より優先)
            foreach (CustomRoles role in Enum.GetValues(typeof(CustomRoles)))
            {
                if (!includeVanilla && role.IsVanilla() && role != CustomRoles.GuardianAngel) continue;
                if (input == GuessManager.ChangeNormal2Vanilla(role))
                {
                    output = role;
                    return true;
                }
            }

            foreach (CustomRoles role in Enum.GetValues(typeof(CustomRoles)))

            {

                if (!includeVanilla && role.IsVanilla() && role != CustomRoles.GuardianAngel) continue;

                var roleName = GuessManager.ChangeNormal2Vanilla(role);

                if (input == roleName

                    || (!string.IsNullOrEmpty(romajiInput)

                        && (romajiInput == NormalizeRomajiInput(role.ToString())

                            || romajiInput == NormalizeRomajiInput(GetString(role.ToString()))

                            || romajiInput == NormalizeRomajiInput(roleName))))

                {

                    output = role;

                    return true;

                }

            }

            return false;

        }

}
public static class GuessManager {
    private static bool CheckTargetRoles(PlayerControl target, CustomRoles role)

    {

        if (MagicalGirl.IsMatchedGuess(target, role)) return false;

        if (role is CustomRoles.SatsumatoImo

            && target.GetCustomRole() is CustomRoles.SatsumatoImoC or CustomRoles.SatsumatoImoM)

        {

            return false;

        }



        bool result = !target.Is(role);



        switch (target.GetCustomRole())

        {

            case CustomRoles.Engineer:

                if (role == CustomRoles.Engineer) result = false;

                break;

            case CustomRoles.Scientist:

                if (role == CustomRoles.Scientist) result = false;

                break;

            case CustomRoles.Tracker:

                if (role == CustomRoles.Tracker) result = false;

                break;

            case CustomRoles.Noisemaker:

                if (role == CustomRoles.Noisemaker) result = false;

                break;

            case CustomRoles.Detective:

                if (role == CustomRoles.Detective) result = false;

                break;

            case CustomRoles.Impostor:

                if (role == CustomRoles.Impostor) result = false;

                break;

            case CustomRoles.Shapeshifter:

                if (role == CustomRoles.Shapeshifter) result = false;

                break;

            case CustomRoles.Phantom:

                if (role == CustomRoles.Phantom) result = false;

                break;

            case CustomRoles.Viper:

                if (role == CustomRoles.Viper) result = false;

                break;

        }



        return result;

    }



    private static bool MsgToPlayerAndRole(string msg, out byte id, out CustomRoles role, out string error)

    {

        id = byte.MaxValue;

        string[] args = msg.Split(' ');



        if (args[0] != "/cmd" || args.Length <= 1)

        {

            role = CustomRoles.NotAssigned;

            id = byte.MaxValue;

            error = "";

            return false;

        }

        args = args.Skip(1).ToArray();



        var result = args.Length < 2 ? "" : args[1];

        msg = args.Length < 3 ? "" : args[2];



        if (int.TryParse(result, out int num))

        {

            id = Convert.ToByte(num);

        }

        else if (result.Contains("レッド") || result.Contains("赤") || result.ToLower().Contains("red"))

        {

            foreach (var pc in PlayerCatch.AllPlayerControls)

            {

                if (pc.cosmetics.ColorId == (int)ModColors.PlayerColor.Red)

                    id = pc.PlayerId;

            }

        }

        else if (result.Contains("ブルー") || result.Contains("青") || result.ToLower().Contains("blue"))

        {

            foreach (var pc in PlayerCatch.AllPlayerControls)

            {

                if (pc.cosmetics.ColorId == (int)ModColors.PlayerColor.Blue)

                    id = pc.PlayerId;

            }

        }

        else if (result.Contains("ライム") || (result.Contains("黄") && result.Contains("緑")) || result.ToLower().Contains("lime"))

        {

            foreach (var pc in PlayerCatch.AllPlayerControls)

            {

                if (pc.cosmetics.ColorId == (int)ModColors.PlayerColor.Lime)

                    id = pc.PlayerId;

            }

        }

        else if (result.Contains("グリーン") || result.Contains("緑") || result.ToLower().Contains("green"))

        {

            foreach (var pc in PlayerCatch.AllPlayerControls)

            {

                if (pc.cosmetics.ColorId == (int)ModColors.PlayerColor.Green)

                    id = pc.PlayerId;

            }

        }

        else if (result.Contains("ピンク") || result.Contains("桃") || result.ToLower().Contains("pink"))

        {

            foreach (var pc in PlayerCatch.AllPlayerControls)

            {

                if (pc.cosmetics.ColorId == (int)ModColors.PlayerColor.Pink)

                    id = pc.PlayerId;

            }

        }

        else if (result.Contains("オレンジ") || result.Contains("橙") || result.ToLower().Contains("orange"))

        {

            foreach (var pc in PlayerCatch.AllPlayerControls)

            {

                if (pc.cosmetics.ColorId == (int)ModColors.PlayerColor.Orange)

                    id = pc.PlayerId;

            }

        }

        else if (result.Contains("イエロー") || result.Contains("黄色") || result.ToLower().Contains("yellow"))

        {

            foreach (var pc in PlayerCatch.AllPlayerControls)

            {

                if (pc.cosmetics.ColorId == (int)ModColors.PlayerColor.Yellow)

                    id = pc.PlayerId;

            }

        }

        else if (result.Contains("ブラック") || result.Contains("黒") || result.ToLower().Contains("black"))

        {

            foreach (var pc in PlayerCatch.AllPlayerControls)

            {

                if (pc.cosmetics.ColorId == (int)ModColors.PlayerColor.Black)

                    id = pc.PlayerId;

            }

        }

        else if (result.Contains("ホワイト") || result.Contains("白") || result.ToLower().Contains("white"))

        {

            foreach (var pc in PlayerCatch.AllPlayerControls)

            {

                if (pc.cosmetics.ColorId == (int)ModColors.PlayerColor.white)

                    id = pc.PlayerId;

            }

        }

        else if (result.Contains("紫") || result.ToLower().Contains("purple") || result.Contains("パープル"))

        {

            foreach (var pc in PlayerCatch.AllPlayerControls)

            {

                if (pc.cosmetics.ColorId == (int)ModColors.PlayerColor.Purple)

                    id = pc.PlayerId;

            }

        }

        else if (result.Contains("ブラウン") || result.Contains("茶") || result.ToLower().Contains("brown"))

        {

            foreach (var pc in PlayerCatch.AllPlayerControls)

            {

                if (pc.cosmetics.ColorId == (int)ModColors.PlayerColor.Brown)

                    id = pc.PlayerId;

            }

        }

        else if (result.Contains("シアン") || result.Contains("水") || result.ToLower().Contains("cyan"))

        {

            foreach (var pc in PlayerCatch.AllPlayerControls)

            {

                if (pc.cosmetics.ColorId == (int)ModColors.PlayerColor.Cyan)

                    id = pc.PlayerId;

            }

        }

        else if (result.Contains("小豆") || result.Contains("マルーン") || result.ToLower().Contains("maroon"))

        {

            foreach (var pc in PlayerCatch.AllPlayerControls)

            {

                if (pc.cosmetics.ColorId == (int)ModColors.PlayerColor.Maroon)

                    id = pc.PlayerId;

            }

        }

        else if (result.Contains("薄桃") || result.Contains("ローズ") || result.ToLower().Contains("rose"))

        {

            foreach (var pc in PlayerCatch.AllPlayerControls)

            {

                if (pc.cosmetics.ColorId == (int)ModColors.PlayerColor.Rose)

                    id = pc.PlayerId;

            }

        }

        else if (result.Contains("ばなーぬ") || result.ToLower().Contains("banana") || result.Contains("バナナ"))

        {

            foreach (var pc in PlayerCatch.AllPlayerControls)

            {

                if (pc.cosmetics.ColorId == (int)ModColors.PlayerColor.Banana)

                    id = pc.PlayerId;

            }

        }

        else if (result.Contains("灰") || result.Contains("グレ－") || result.ToLower().Contains("gray"))

        {

            foreach (var pc in PlayerCatch.AllPlayerControls)

            {

                if (pc.cosmetics.ColorId == (int)ModColors.PlayerColor.Gray)

                    id = pc.PlayerId;

            }

        }

        else if (result.ToLower().Contains("tan") || result.Contains("タン"))

        {

            foreach (var pc in PlayerCatch.AllPlayerControls)

            {

                if (pc.cosmetics.ColorId == (int)ModColors.PlayerColor.Tan)

                    id = pc.PlayerId;

            }

        }

        else if (result.ToLower().Contains("coral") || result.Contains("珊瑚") || result.Contains("コーラル"))

        {

            foreach (var pc in PlayerCatch.AllPlayerControls)

            {

                if (pc.cosmetics.ColorId == (int)ModColors.PlayerColor.Coral)

                    id = pc.PlayerId;

            }

        }

        else

        {

            id = byte.MaxValue;

            error = GetString("GuessError1");

            role = new();

            return false;

        }



        PlayerControl target = PlayerCatch.GetPlayerById(id);

        if (target == null || target.Data.IsDead)

        {

            error = GetString("GuessError2");

            role = new();

            return false;

        }



        if (!UtilsRoleInfo.GetRoleByInputName(msg, out role, true))

        {

            error = GetString("GuessError1");

            return false;

        }



        error = string.Empty;

        return true;

    }



    public static string ChangeNormal2Vanilla(CustomRoles role)

    {

        switch (role)

        {

            case CustomRoles.Engineer:

                role = CustomRoles.Engineer;

                break;

            case CustomRoles.Scientist:

                role = CustomRoles.Scientist;

                break;

            case CustomRoles.Tracker:

                role = CustomRoles.Tracker;

                break;

            case CustomRoles.Noisemaker:

                role = CustomRoles.Noisemaker;

                break;

            case CustomRoles.Detective:

                role = CustomRoles.Detective;

                break;

            case CustomRoles.Impostor:

                role = CustomRoles.Impostor;

                break;

            case CustomRoles.Shapeshifter:

                role = CustomRoles.Shapeshifter;

                break;

            case CustomRoles.Phantom:

                role = CustomRoles.Phantom;

                break;

            case CustomRoles.Viper:

                role = CustomRoles.Viper;

                break;

        }



        return GetString(Enum.GetName(typeof(CustomRoles), role)).TrimStart('*').ToLower().Trim().Replace(" ", string.Empty).RemoveHtmlTags();

    }


public static bool Parse(string text,out byte id,out CustomRoles role,out string error)=>MsgToPlayerAndRole(text,out id,out role,out error);
public static bool Wrong(PlayerControl target,CustomRoles role)=>CheckTargetRoles(target,role);
}
public static class TellerGate { public static Option TellerCannotGuess;
    static readonly CustomRoles[] TellerRoles =
    {
        CustomRoles.FortuneTeller, CustomRoles.AmateurTeller, CustomRoles.PonkotuTeller,
        CustomRoles.SuspiciousTeller, CustomRoles.MadTeller, CustomRoles.EvilTeller,
    };
    // 「占い系はゲッサーを使えない」がONで、その人が占い系か
    public static bool IsBlockedTeller(CustomRoles role)
        => TellerCannotGuess?.GetBool() == true && System.Array.IndexOf(TellerRoles, role) >= 0;
}
}
