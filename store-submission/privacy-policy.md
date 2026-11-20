# SoundSwitch Tray Privacy Policy

Effective date: November 19, 2026  
Publisher: Munawer Hussain

SoundSwitch Tray runs 100% offline. It collects zero personal data and makes zero network requests. It has no telemetry, analytics, advertising, accounts, or online services. It does not transmit, sell, or share any data.

The app reads Windows playback device identifiers, names, and the current default playback device locally to display its tray icon and let you cycle audio outputs. It does not record audio or access microphone recordings.

The only information the app stores is local audio device color preferences in `%LOCALAPPDATA%\SoundSwitch\colors.xml`. This file pairs a local Windows audio device identifier with its assigned tray icon color. It contains no usage history or audio content. Windows may virtualize this local path for the Microsoft Store package.

Preferences stay on your computer and are never uploaded. To reset them, exit SoundSwitch Tray and delete `colors.xml`; the app creates fresh color assignments when it next runs. Windows controls the installation and removal of the Store package and any cleanup of its local app data.

Changing the default playback device happens only in response to your click on the tray icon. The app runs as the current user without requesting administrator elevation.
