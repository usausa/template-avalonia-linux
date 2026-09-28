# キオスク端末にする

## 1. OS 設定

### 自動ログイン

`/etc/gdm3/custom.conf` の `[daemon]` に記述。

**設定**

```ini
[daemon]
AutomaticLoginEnable=true
AutomaticLogin=<ユーザー>
```

## 2. GNOME 設定

### 画面の消灯・スクリーンセーバー・ロック

**設定**

```bash
gsettings set org.gnome.desktop.session idle-delay 0
gsettings set org.gnome.desktop.screensaver idle-activation-enabled false
gsettings set org.gnome.desktop.screensaver lock-enabled false
gsettings set org.gnome.desktop.lockdown disable-lock-screen true
```

### スリープ・画面を暗くする

**設定**

```bash
gsettings set org.gnome.settings-daemon.plugins.power sleep-inactive-ac-type 'nothing'
gsettings set org.gnome.settings-daemon.plugins.power sleep-inactive-battery-type 'nothing'
gsettings set org.gnome.settings-daemon.plugins.power idle-dim false
```

### ホットコーナー

**設定**

```bash
gsettings set org.gnome.desktop.interface enable-hot-corners false
```

### Super キーの概要

**設定**

```bash
gsettings set org.gnome.mutter overlay-key ''
```

### 仮想端末の切り替え(Ctrl+Alt+F1〜F12)

**設定**

```bash
for i in $(seq 1 12); do gsettings set org.gnome.mutter.wayland.keybindings switch-to-session-$i '[]'; done
```

### Ctrl+Alt+Delete のログアウト

**設定**

```bash
gsettings set org.gnome.settings-daemon.plugins.media-keys logout '[]'
```

### 通知のバナー

**設定**

```bash
gsettings set org.gnome.desktop.notifications show-banners false
```

### 画面の自動回転

**設定**

```bash
gsettings set org.gnome.settings-daemon.peripherals.touchscreen orientation-lock true
```

### 更新の通知と更新の画面

**設定**

```bash
gsettings set com.ubuntu.update-notifier no-show-notifications true
mkdir -p ~/.config/autostart
cp /etc/xdg/autostart/update-notifier.desktop ~/.config/autostart/
echo 'Hidden=true' >> ~/.config/autostart/update-notifier.desktop
```

### クラッシュの報告の通知

**設定**

```bash
gsettings set com.ubuntu.update-notifier show-apport-crashes false
```

## 3. デプロイ

### 配置

```bash
dotnet publish src/Template.LinuxApp/Template.LinuxApp.csproj -c Release -f net10.0 -r linux-x64 -p:PublishSingleFile=true --self-contained -o publish
ssh <ユーザー>@<端末> mkdir -p Template.LinuxApp
scp publish/* <ユーザー>@<端末>:Template.LinuxApp/
ssh <ユーザー>@<端末> chmod +x Template.LinuxApp/Template.LinuxApp
```

- 設定: `~/Template.LinuxApp/appsettings.Production.json`

### PIN

**設定**

```json
{ "Kiosk": { "AdminPin": "5678" } }
```

- 既定の `1234` は仮の値
- 終了: Dashboard の Exit → PIN、ゲームパッドは Back + A を 3 秒 → A・B・X・Y

## 4. 自動起動

`~/.config/systemd/user/template-linuxapp.service` を作成。

```ini
[Unit]
Description=Template.LinuxApp
PartOf=graphical-session.target
After=graphical-session.target

[Service]
Type=simple
WorkingDirectory=%h/Template.LinuxApp
ExecStart=%h/Template.LinuxApp/Template.LinuxApp
Environment=DOTNET_ENVIRONMENT=Production
Restart=on-failure
RestartSec=5

[Install]
WantedBy=graphical-session.target
```

- アプリを終了するとデスクトップに戻る(落ちたときは 5 秒後に起動し直す)

## 6. 終了時

アプリを終了したとき、デスクトップに戻らず処理する方法。

### 終了したらログアウト

`template-linuxapp.service` の `ExecStart` を書き換える。

**設定**

```ini
ExecStart=/bin/sh -c '%h/Template.LinuxApp/Template.LinuxApp && gnome-session-quit --logout --no-prompt'
```

### ログアウトのあと自動でログイン

`/etc/gdm3/custom.conf` の `[daemon]` に追記。

**設定**

```ini
TimedLoginEnable=true
TimedLogin=<ユーザー>
TimedLoginDelay=5
```

## 6. SSH からの起動

SSH で接続してサービスを使わずに起動する方法。
```bash
systemd-run --user --unit=template-linuxapp --working-directory=$HOME/Template.LinuxApp $HOME/Template.LinuxApp/Template.LinuxApp
```
