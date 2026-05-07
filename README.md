
SmartTaskbar++  <img src="https://github.com/ChanpleCai/SmartTaskbar/blob/main/logo/logo.png" width="24">
=====

* SmartTaskbar++ is a lightweight utility which can automatically switch the display state of the Windows Taskbar and apply global screen color effects.

Features
-----

#### Auto Mode (SmartTaskbar)

* In the Auto Mode, SmartTaskbar will set the Taskbar to hide when the focused window and the taskbar intersect.
  
* Double-click the tray icon to switch the display status of the taskbar between Show or Auto-Hide.

#### Screen Effects (Integrated from NegativeScreen)

* Apply global color filters to your display, including:
    * **Negative Mode**: Classic color inversion.
    * **Night Mode (Negative Red)**: Inverted colors with a red filter for eye comfort.
    * **Smart Inversion**: Modern hue-shifting logic that preserves natural colors while inverting brightness.
    * Grayscale, Sepia, and more.

Credits
----

This application is a combined work of two excellent utilities:

* **SmartTaskbar**: Original taskbar management logic by [ChanpleCai](https://github.com/ChanpleCai/SmartTaskbar).
* **NegativeScreen**: Global screen inversion and color matrix engine by [Melvyn Laïly](https://github.com/mlaily/NegativeScreen).

License
----

This project is licensed under the **GNU General Public License v3.0** (GPL-3.0) due to the integration of NegativeScreen's core logic. 

* The original SmartTaskbar code by ChanpleCai was MIT licensed.
* The NegativeScreen code by Melvyn Laïly is GPL-3.0 licensed.
* As a derivative work, this combined application is distributed under GPL-3.0.

Build
-----
* Visual Studio 2022.

Notice
------
* The status of the taskbar does not change when the mouse is over the taskbar.  

* The [Microsoft Store](https://www.microsoft.com/en-us/p/smarttaskbar/9pjm69mps6t9?activetab=pivot%3aoverviewtab) version is slightly less functional and stable.
