# ExtralityLab - Websockets `Documentation outdated!` ⚠️

This repository was created as an initial template for students to use when developing basic applications with IoT devices, such as the ESP32, Meta Quest HMDs, and the Unity Game Engine. Its contents serve as a tutorial for future tasks in the Design for Emerging Technologies (DET) course at Stockholm University.

# Unity Instructions

### Once Unity is installed and the project opened...

- If you get prompted to restart the Unity Editor select "Ignore"
- If you get prompted to choose the OVR hand, select either "Remind me later" or "Keep using OVR Hand"
- Go to "File"-> "Build Settings". Once a new window opens, locate and click on "Android" under "Platform" on the left side of the window. Once that is done click on "Switch Platform". Once that is done you can close the "Build Settings" window.
  - ***Note: If the "Android" option is greyed out, it means you don't have the required modules installed. Refer back to the "Before you start" section to solve this***
- Once you have done finished these steps, open either the "WebSocket Client Example" or "WebSocket Client Example XR" scenes.

### Setting up the project

- Once you have one of the scenes open, locate the "WebSocketClient Manager" GameObject. It should have a "Web Socket Client" script attached to it.
- Replace the "IP Address" and "Port" with the right values from the ESP32
  - ***Note: You can get the "IP Address" using the Serial Monitor in the Arduino IDE and the "Port" from the "ESP32_Server.ino" file***

- Assuming your ESP32 is correctly setup, you can now either hit the "Play" button in Unity (if you are using the "WebSocket Client Example" scene) or build a new apk and deploy it to your device (if you are using the "WebSocket Client Example XR")

### Highlights of the project

The "DET Lab Files" folder contains all the required files for this demo.

![Unity Folder](docs~/UnityFolder.png)

There is only 1 script that truly matters and that is the "WebSocketClient.cs" script inside the "Scripts" folder. This is responsible for the communication between the device running Unity and the ESP32. Examine it to better understand how it works.

There are 2 Unity Scenes created for this project.

- "WebSocket Client Example" can be ran without a VR headset and does the exact same thing as the other Scene
- "WebSocket Client Example XR" can only be run with a Meta Quest device (or with a simulator).

 ---
 Made by: António Pinheiro Braga - antonio.braga@dsv.su.se
 Code based on example provided by: [Anas Kuzechie](https://akuzechie.blogspot.com/2020/12/esp32-websocket-server.html)
