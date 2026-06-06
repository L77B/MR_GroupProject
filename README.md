## Xage



<img width="300" height="250" alt="LogoTransperent" src="https://github.com/user-attachments/assets/7da64607-0b2e-4b01-af38-5121cf900cb0" />

## Introduction -Krish

Explain what your project is about, what problem it solves, and what educational value it provides. You can also include a catchy slogan or a logo for your project.




## Design Process 
Many ideas were brainstormed, but eventually the idea of an MR rageroom was chosen through dotvoting and bodystorming (see picture below).

<img width="500" height="400" alt="Skärmavbild 2026-06-06 kl  12 08 01" src="https://github.com/user-attachments/assets/cbe7e068-7df6-438d-a476-7d4b8118ab71" />

The design requirements were planned based on the body- and brainstorming and written down in a table. Requirements from the project description were also a digital twin, MR, tangibles, multiplayer and co-location. These ideas and requirements were combined in a table with columns such as: weapons, breakable objects, tangible objects, particle effects and sounds (see picture below).


 <img width="562" height="501" alt="Skärmavbild 2026-06-06 kl  10 03 31" src="https://github.com/user-attachments/assets/4b60d2cd-b878-4084-9f66-276c441a7e12" />


#### Here were the goals for the project:
- Multiplayer experience for two players
- Breakable VR objects, both from the asset store and 3D scanning with a deep-sensing camera on physical objects from the lab.
- Tangible buttons that spawn the objects
- Tangible buttons that make an explosion when pressed simultaneously
- Tangible and virtual “ragemeeter” connected to both of the players
  Co-location with QR code
- Hand Interactions
- Different levels on the ragemeter, so new weapons are spawned depending on the level
- 3D printed weapons for more immersion 
- Shoot with the VR gun on a lightbulb so it turns off through mgtt

These goals were all planned in a timeline (see picture below)
<img width="1064" height="371" alt="Skärmavbild 2026-06-06 kl  12 11 28" src="https://github.com/user-attachments/assets/9dfd12a8-bb11-48bb-96ba-85dff95c9a7b" />


#### There were many challenges and solutions in the process:
- The deep-sensing
 camera didn't work

Solved: an app was downloaded, which could scan the objects.

- Colocation -players saw objects in different places

Solved: sometimes it worked, sometimes not.

- Network -actions not synchronised
  
Solved: fixed the networking issues.

- Ragemeter -syncing with objects when they get destroyed
  
Solved: fixed the networking issues, sometimes it works, sometimes not

- Dynamite -not spawning with multiplayer
  
Not solved: this was not solved, the players only see the explosion, not the dynamite before.

- Explosion moving with the player's headset
  
Not solved: it still moved when the headset moved

- Hand interaction is not working
  
Solved: by having the controllers be on a 3D printed baseball bat and paintball gun. 

- MQTT lights interactions weren't fixed

In the end, the scope was cut down compared to our initial design requirements. One of the main issues was the co-location with the hand interactions and objects spawning. And because we changed the controllers to be on the weapons, the different types of levels on the rage meter could not be implemented. Due to time management and implementation issues.




## Features and functionalities of your project -Krish
You can use bullet points, screenshots, gifs, or videos to illustrate your points. Also, include a link to your project's demo or live version.

## Installation -Happy

process to build and run your project. Use code blocks, tables, or lists to show the chosen platform's commands, steps, or requirements. Mention any dependencies or libraries that your project uses and how to install them.
This is a Unity 6 Mixed Reality project targeting Meta Quest headsets. It uses photon for real-time multiplayer, Meta XR SDK for passthrough/MR, and optionally an ESP32 LED strip for physical feedback. There are two components to set up:

1. The Unity app (deployed to Meta Quest via Android)
2. The ESP32 LED strip firmware (optional physical peripheral)

Requirements
Software
| Tool                    | Version / Notes                                          |
|-------------------------|----------------------------------------------------------|
| Unity Editor            | 6000.3.10f1 (exact version required — use Unity Hub)     |
| Android Build Support   | Installed via Unity Hub (includes Android SDK/NDK/JDK)   |
| Meta Quest Link / ADB   | For sideloading to the headset                           |
| Arduino IDE             | v2.x — for the LED strip firmware only                   |
| Git                     | To clone the repo                                        |

Hardware

| Device                              | Purpose                          |
|-------------------------------------|----------------------------------|
| Meta Quest 3 / 3S / Pro             | Running the MR experience        |
| PC running Unity                    | Build machine                    |
| ESP32 board (optional)              | Physical WS2812B LED rage bar    |
| WS2812B LED strip, 60 LEDs (optional) | Connected to ESP32 GPIO 5      |

1. Clone the Repository
git clone <repo-url>
cd MR_GroupProject
Do not open in Unity yet — install the correct Unity version first.

2. Install Unity 6000.3.10f1
    1. Open Unity Hub
    2. Go to Installs → Add → Archive
    3. Search for 6000.3.10f1 or download from Unity's archive
    4. During install, select these modules:
       Android Build Support
          Android SDK & NDK Tools
          OpenJDK
3. Open the Project
 1. In Unity Hub → Projects → Add → select the MR_GroupProject folder
 2. Unity will import all assets and resolve packages automatically — this can take 5–10 minutes on first open

4. Unity Package Dependencies
All packages are declared in Packages/manifest.json and auto-resolved by the Unity Package Manager. Key dependencies:

| Package                             | Version | Purpose                                              |
|-------------------------------------|---------|------------------------------------------------------|
| com.meta.xr.sdk.all                 | 201.0.0 | Meta XR SDK — passthrough, controllers, hand tracking |
| com.unity.xr.openxr                 | 1.16.1  | OpenXR runtime                                       |
| com.unity.xr.meta-openxr            | 2.5.0   | Meta OpenXR extensions                               |
| com.unity.netcode.gameobjects       | 2.11.0  | Unity Netcode — multiplayer                          |
| com.unity.services.multiplayer      | 2.2.1   | Unity Relay/Lobby                                    |
| com.unity.render-pipelines.universal| 17.3.0  | URP rendering                                        |
| com.unity.inputsystem               | 1.18.0  | New Input System                                     |
| com.unity.ai.navigation             | 2.0.10  | NavMesh AI                                           |
| se.su.dsv.extralitylab.unity        | git     | Extralit Lab utilities (fetched from Gitea)          |

No manual npm install or pip install needed — Unity Package Manager handles everything on first open.

5. Configure Meta Quest Link (for Play Mode testing)
To test in the editor via Oculus Link / Air Link:

Enable Developer Mode on your Quest headset (via the Meta mobile app)
Connect via USB or Air Link
In Unity: Edit → Project Settings → XR Plug-in Management → confirm Meta OpenXR is checked for Android
6. Build & Deploy to Meta Quest
Configure Build Settings
File → Build Settings
Set platform to Android (click Switch Platform if not already set)
Add the main scene — Assets/Scenes/Xage 1.unity (or the scene your team uses)
Set Player Settings
Edit → Project Settings → Player → Android tab
Package name: set to something like com.yourteam.xage
Minimum API Level: API 29 (Quest minimum)
Target API Level: API 34
Scripting Backend: IL2CPP
Target Architecture: ARM64 only
Build APK
File → Build Settings → Build
Save as e.g. Xage.apk

Sideload to Headset
With Quest connected via USB and Developer Mode on:

adb install -r Xage.apk
Or use Meta Quest Developer Hub (MQDH) for a GUI sideload.

7. Multiplayer Setup (Unity Netcode + Relay)
The project uses Unity Gaming Services for Relay/Lobby. Both players need:

A Unity project linked to a Unity Dashboard Organization (the project already has UnityConnectSettings.asset configured)
Both headsets on the same Wi-Fi network (for colocation)
One player hosts, one joins — handled by Assets/Scripts/NetworkSessionManager.cs and Assets/Scripts/ColocationSetup.cs
8. ESP32 LED Strip (Optional Physical Peripheral)
The file ESP32_LED/RageLEDStrip.ino drives a 60-LED WS2812B strip that mirrors the in-game rage bar.

Arduino Library Dependencies
Install via Arduino IDE → Library Manager (Ctrl+Shift+I):

Library	Purpose
FastLED	WS2812B LED control
WebSocketsServer (arduinoWebSockets by Links2004)	WebSocket server so Unity can push rage values
Also install ESP32 board support:

File → Preferences → Additional Board URLs:
https://raw.githubusercontent.com/espressif/arduino-esp32/gh-pages/package_esp32_index.json
Tools → Board Manager → search esp32 → install esp32 by Espressif
Configure & Flash
Edit the top of ESP32_LED/RageLEDStrip.ino:

const char* SSID     = "YOUR_WIFI_SSID";      // your Wi-Fi name
const char* PASSWORD = "YOUR_WIFI_PASSWORD";  // your Wi-Fi password

#define LED_PIN   5    // GPIO pin to DATA line of LED strip
#define NUM_LEDS  60   // number of LEDs on your strip
Then:

Select your board: Tools → Board → ESP32 Dev Module (or your specific ESP32 variant)
Select the correct COM port: Tools → Port
Click Upload
The ESP32 listens on port 8082 via WebSocket. Unity's Assets/Scripts/RageLEDClient.cs connects to it automatically when running.

Quick Reference Summary
1. Clone
git clone <repo-url>
2. Open in Unity 6000.3.10f1 (let packages resolve)
3. Build Settings → Android → Switch Platform
4. Build APK
   File → Build Settings → Build → Xage.apk
5. Sideload
adb install -r Xage.apk
6. (Optional) Flash ESP32
   Edit WiFi credentials in RageLEDStrip.ino → Upload via Arduino IDE


## Usage section 

Type of interaction: hand controller

Weapon 1: baseball bat/lightsaber

The virtual baseball bat follows the movement on the left-hand controller and could be changed to a lightsaber by pressing Y. You hit the virtual objects as you would with a reel baseball bat with haptic feedback. 


Weapon 2: Paintball gun

The paintball gun follows the movement of the right controller and shoots paint with the trigger button, there is haptic feedback when shooting.
When objects are destroyed by the players the rage meter changes and, but it goes down when no objects break. When the rage meter is full of rage, the players can make an explosion happen by pressing two buttons simultaneously. 

The destructible objects are spawned when either the green or blue button is pressed (see picture below).

<img width="350" height="500" alt="Skärmavbild 2026-06-06 kl  12 23 47" src="https://github.com/user-attachments/assets/78ac3723-60e1-470c-9872-7abfe31fb39e" />




## Contributors -Happy
or maintainers of your project and how to contact them.

