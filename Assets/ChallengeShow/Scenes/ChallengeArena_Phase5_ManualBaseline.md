# Phase 5 manual baseline — captured before physics stabilization

The scene as manually adjusted by the user is authoritative. This file records every
scene-level presentation value that a builder command could otherwise overwrite, so the
look can be restored by hand if anything is lost.

- Locked scene copy: `Assets/ChallengeShow/Scenes/ChallengeArena_Phase5_ManualBaseline.unity`
- Live scene: `Assets/ChallengeShow/Scenes/ChallengeArena.unity`
- No git repository in this project, so the lock is a file-level copy.

## Scene roots

```
ChallengeArena          (generated root - Lighting, CloudSea, Environment, LaneGameplay,
                         CrystalSpikeWall, RotatingArmObstacle, Systems, Cameras, VFX, Presentation)
Global Volume           (MANUALLY ADDED - not created by any builder)
```

## RenderSettings  — MANUALLY CHANGED, builder would overwrite

| Setting | Current (manual) | What BuildLighting would write |
|---|---|---|
| skybox | `Assets/Shaders/New Material.mat` | `Ilumisoft/Mountain Valley/Materials/Skybox.mat` |
| fog | **False** | `true` (Linear, 70..230) |
| sun color | `(1, 1, 1)` white | `(1, 0.96, 0.87)` warm |
| ambientMode | Trilight | Trilight (same) |
| ambientSky | (0.620, 0.740, 0.900) | same |
| ambientEquator | (0.520, 0.580, 0.620) | same |
| ambientGround | (0.300, 0.320, 0.360) | same |
| ambientIntensity | 1 | not written |
| fogColor / start / end | (0.62,0.78,0.90) / 70 / 230 | same values, but fog re-enabled |
| reflectionIntensity | 1 | not written |

## Sun

`type=Directional  rot=(48, 325, 0)  color=white  intensity=1.5  shadows=Soft  bounce=1`

## Global Volume — MANUALLY ADDED

Profile: `Assets/ChallengeShow/Scenes/ChallengeArena/Global Volume Profile.asset`
`isGlobal=true  priority=0  weight=1`

**OutlineVolume** (active)
```
isActive=True  mode=FullScreen  outlineColor=(0,0,0,1)  thickness=1  outlineIntensity=1
useDepth=True  depthThreshold=0.5  normalThreshold=0.35
depthViewBias=0.5  normalViewBias=0.5
```

**Bloom** (active)
```
skipIterations=1  threshold=0.9  intensity=0  scatter=0.7  clamp=65472
tint=(1,1,1,1)  highQualityFiltering=False  filter=Gaussian  dirtIntensity=0
```

## CloudSea — MANUALLY RETUNED, builder would overwrite

`pos=(0, -3, 26)  scale=(420, 26, 420)`  — builder places it at `y = -16.5`

```
quality=1              litColor=(1.000, 0.990, 0.970)
density=8              shadowColor=(0.420, 0.527, 0.692)
coverage=0.5           noiseScale=0.005      secondaryNoiseScale=0.005
distortion=0.351       edgeFade=0.392        verticalFade=0.6
lightInfluence=0.264   maxMarchDistance=200  scrollSpeed=0.4
noiseTexture=CloudNoise
material=Assets/ChallengeShow/Materials/CloudSea.mat  (_StepCount=15, _Jitter=0.85, _UseSecondary=1)
```

## Camera

`pos=(-15, 27, -33)  rot=(20.963, 15.101, 0)  fov=46  far=900`

```
establishingPosition=(-15,27,-33)  establishingLookAt=(2,2,30)  establishingFov=46
sideAngle=78     followDistance=11   followHeight=4      followFov=50
lookHeightOffset=1.2                 ragdollDistance=13  ragdollHeight=5.5
recoverDistance=9  recoverHeight=4.6 resultDistance=8    resultHeight=4.2
positionSharpness=3.5  aimSharpness=5  orbitDriftSpeed=4
alternateSides=True    startOnRight=True
obstructionProbeRadius=0  minObstructedDistance=7.5
smallUnitHeight=1.5    smallUnitDistanceScale=0.78
```

## Environment transforms

### Lane macros
```
ImpactZone         (0, 0,  0)
SpawnCourt         (0, 0,  9)
ArmZone            (0, 0, 18)
LaneStraight_12m   (0, 0, 30)
FinishCourt        (0, 0, 42)
LaneStraight_6m    (0, 0, 51)
```

### Bastions  — yaw values are FINAL world rotations, not recipe offsets
```
Bastion_Cactus     pos=(-31,  9, 29)  rotY= 84.53  scale=1.10
Bastion_Cat        pos=(-25,  2,  3)  rotY= 62.39  scale=1.00
Bastion_Dog        pos=(  5, 13, 78)  rotY=179.49  scale=1.70
Bastion_MoleRat    pos=( 32,  7, 36)  rotY=237.65  scale=1.15
Bastion_Skeleton   pos=( 24,  3, -1)  rotY=328.37  scale=1.05
```

### Landmarks
```
FinishGate         pos=(0, 0, 42)  rotY=180
```

## Gameplay collision — must remain exactly this

```
LaneCollision   pos=(0, -0.5, 22)  scale=(9, 1, 56)
FinishTrigger   pos=(0,  3,  42)
FailVolume      pos=(0, -40, 20)
CrystalSpikeWall pos=(0, 0, 6.1)   (Backstop + ImpactFace children)
RotatingArmObstacle pos=(0, 4.6, 16)
```
