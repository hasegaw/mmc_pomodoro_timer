# Pomodoro Timer

OBSのウィンドウキャプチャで使う、Windows向けのポモドーロタイマーです。

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

## 操作

- 時刻をクリック: 停止中に `MM:SS` または `HH:MM:SS` を直接入力し、Enterで確定（Escで取消）
- マウスをウィンドウ内へ移動: リセット、スタート／ストップボタンを表示
- 背景部分を左ドラッグ: タイトルバーのないウィンドウを移動
- 右クリック: フォント選択または終了

設定した開始時刻、フォント、ウィンドウ位置は `pomodoro.ini` に保存されます。
