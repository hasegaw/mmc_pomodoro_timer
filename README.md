<p align="center">
  <img src="manual/assets/logo.png" width="128" height="128" alt="Pomodoro Timer アプリロゴ">
</p>

# Martian Midnight Company's Pomodoro Timer

OBSのウィンドウキャプチャで使える、Windows向けのポモドーロタイマーです。深夜カンパニー日常業務用に最適。

## 起動方法

PomodoroTimer.exe を起動してください。

## 操作

- 時刻をクリック: 停止中に `MM:SS`、`HH:MM:SS`、`MMSS`、`HHMMSS` を直接入力し、Enterで確定（Escで取消）
- マウスをウィンドウ内へ移動: リセット、スタート／ストップボタンを表示
- 背景部分を左ドラッグ: タイトルバーのないウィンドウを移動
- 右クリック: フォント選択、アラーム音、最前面表示、縮小表示の有効／無効切り替え、または終了

設定した開始時刻、フォント、ウィンドウ位置、アラーム音、最前面表示、縮小表示の有効／無効は `pomodoro.ini` に保存されます。縮小表示ではクライアント領域が250px × 250pxになります。

起動中のタイマーは、別プロセスから次のコマンドで操作できます。
StreamDeck からの制御に使えます。

```powershell
PomodoroTimer.exe /start
PomodoroTimer.exe /stop
PomodoroTimer.exe /click
PomodoroTimer.exe /reset
PomodoroTimer.exe /set 2500
PomodoroTimer.exe /start /set 2500
PomodoroTimer.exe /stop /set 2500
PomodoroTimer.exe /reset /set 25:00
```

`/set` は `/start`、`/stop`、`/reset` と同時に指定でき、時間更新と稼働状態の設定を1回で行えます。指定順は問いません。たとえば `/start /set 25:00` は25分に更新して開始し、`/reset /set 25:00` は開始時間と残り時間を25分に更新して停止状態にします。Stream Deck の「開く」アクションにも同じ複合コマンドを登録できます。`/click` と `/set` は同時に指定できません。

## ビルド

Visual Studio 2022で `PomodoroTimer.sln` を開き、`PomodoroTimer` をスタートアッププロジェクトにして実行します。
コマンドラインでは次のようにビルドできます。

```powershell
dotnet build PomodoroTimer.sln
```

配布用の単一exeは次のコマンドで作成できます。

```powershell
dotnet publish src\PomodoroTimer\PomodoroTimer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

`background.png`、通知音ファイル、実行時に作られる `pomodoro.ini` はexeと同じフォルダーに置きます。
通知音は `timer.mp4`、`timer.mp3`、`timer.wav` の順に検索されます。標準では `timer.mp3` を同梱します。

### 配布ZIPの作成

タグを付けたコミットで次のコマンドを実行すると、自己完結型の単一exe、標準の画像・通知音、`manual`
フォルダー、`LICENSE` をまとめた `artifacts/mmc_pomodoro_timer_タグ_YYMMDD_HHMM.zip`（日時は日本時間）を作成します。

```powershell
.\tools\package.ps1
```

未タグのコミットで試す場合は、`.\tools\package.ps1 -VersionTag dev` のようにタグ名を明示できます。

`v` で始まるタグ（例: `v1.0.0`）をpushすると、GitHub Actions の **Build Windows package** が実行されます。
作成されるファイル名は、たとえば `mmc_pomodoro_timer_v1.0.0_260904_0145.zip` です。ZIPはActionsの
成果物に保存され、そのタグのGitHub Releaseにも添付されます。

#### 開発者向けリリース手順

リリース対象の変更をコミットした後、そのコミットにバージョンタグを付けてGitHubへpushします。

```powershell
git tag v1.0.0
git push origin v1.0.0
```

pushしたタグを起点として、GitHub Actionsが配布ZIPをビルドします。リリースごとに `v1.0.1` のように
新しいバージョン番号を指定してください。

## ライセンス

このプロジェクトは MIT License のもとで公開されています。
個人利用・仕事での利用を問わず、無料で使ったり、コピー・変更・再配布したりできます。
再配布する場合は、元の著作権表示と MIT License の文章を一緒に残してください。
