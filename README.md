# Shooting Game

## Introduction to Game Development

**Student:** Karina Tulegenova  
**Group:** SE-2419  
**Project Name:** Shooting Game
DEPLOY: https://karinatulegenova.itch.io/shootgame
---

# 1. Introduction

This project presents the development of a 3D shooter game created in Unity.

The main objective of the game is to control a player character, eliminate enemies using shooting mechanics, and achieve the highest possible score while avoiding collisions with enemies.

The project demonstrates fundamental game development concepts, including:

- Player movement
- Shooting systems
- Collision detection
- User interface design
- Game state management

---

# 2. Game Overview

## 2.1 Concept

The game is a 3D action shooter where the player navigates through an environment and eliminates enemies.

## 2.2 Objective

The player must:

- Destroy enemies to gain points
- Avoid collisions with enemies
- Survive as long as possible or eliminate all enemies

## 2.3 Game Features

- Player movement (WASD / Arrow keys)
- Shooting system
- Enemy spawning
- Score tracking
- Win and Lose conditions
- Restart system
- Dual camera system (First-person and Third-person)

---

# 3. System Design

## 3.1 Scene Structure

### Intro Screen

The Intro Screen serves as the main entry point of the game and provides the player with initial interaction options.

- Play → starts the game
- Controls → shows game controls and interaction information



---

### Main Scene

The Main Scene represents the core gameplay environment where the player interacts with the game mechanics.



---

### Lose Scene

Displayed when the player loses the game.



---

### Win Scene

Displayed when the player completes the game successfully.



---

## 3.2 Player System

The player character includes:

- Rigidbody for physics
- Movement script
- Shooting functionality



---

## 3.3 Enemy System

Enemies are designed to:

- Spawn randomly
- Move or interact with the player
- Trigger game over on collision


---

## 3.4 Shooting Mechanic

The shooting system includes:

- Weapon attached to the player
- Bullet instantiation
- Collision detection with enemies
- Explosion effects on impact



---

## 3.5 Scoring System

- Score increases when enemies are destroyed
- Displayed using UI Text element



---

# 4. Implementation

## 4.1 Scripts Overview

The following scripts were implemented:

- `PlayerController` — handles movement and input
- `ShootingScript` — manages shooting mechanics
- `EnemyScript` — controls enemy behavior
- `GameManager` — manages game states and score
- `UIManager` — updates UI elements


---

# Conclusion

This project provided practical experience in developing a 3D shooter game using Unity.

The project helped in understanding:

- Game logic
- Physics systems
- UI integration
- Interaction systems

Future improvements may include:

- Advanced enemy AI
- Additional levels
- Enhanced visual effects

