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

