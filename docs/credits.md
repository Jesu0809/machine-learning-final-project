# Credits

Night Shift is a university project (Machine Learning course, Universidad de Cundinamarca). Third-party
assets are used under the licenses below.

## Freddy

"[Freddy Fazbear](https://sketchfab.com/3d-models/freddy-fazbear-e8248c29265544e7b7e686b4c0c09f10)" by
2012carlitos17, [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/), rigged and animated with Adobe
Mixamo. Fan work, non-commercial. Freddy Fazbear © Scott Cawthon; not affiliated with the original games.

## 3D models (Sketchfab)

Downloaded from Sketchfab and prepared for the game by `tools/prepare_models.py` (rescaled to meters,
turned to face +Z, moving parts split into their own nodes, textures resized). The prepared files are in
`unity/Assets/Art/Sketchfab/`.

| Model | Author | License | Used as |
| --- | --- | --- | --- |
| [Fairheaven SW '84 - Low poly model](https://sketchfab.com/3d-models/aa0becb6e854422596cac6b21bf79787) | Daniel Zhabotinsky | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) | The guard's car (`car_wagon.glb`) |
| [Old Rusty Car](https://sketchfab.com/3d-models/95baa20ebc5d4d2e869f0b549be838fe) | Renafox (@kryik1023) | [CC BY-NC 4.0](https://creativecommons.org/licenses/by-nc/4.0/) | The wreck in the back lot (`car_wreck.glb`) |
| [Industrial Electricity's Lever](https://sketchfab.com/3d-models/66915915e3234d319415d6bfb4e3a59e) | Amine.Elouneg | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) | The exit lever (`exit_lever.glb`) |
| [First Person hands rigged](https://sketchfab.com/3d-models/547a45535f0c4fe787948f7a7a6a88db) | David Fischer (@davidfischer) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) | First-person arms (`fp_hands.glb`) |
| [Early 1900s Knife Switches Type C](https://sketchfab.com/3d-models/033dd717e4b04e46b6da85195085b8f3) | Mad_Lobster_Workshop | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) | The breaker box's main switch (`knife_switch.glb`) |

The Old Rusty Car is licensed for non-commercial use only; the game is a non-commercial student project.
A commercial release would have to replace it.

## Props and surfaces

The props of the pizzeria (`tools/fetch_models.py`) and the scanned surfaces (`tools/fetch_surfaces.py`)
come from [Poly Haven](https://polyhaven.com) under [CC0](https://creativecommons.org/publicdomain/zero/1.0/).

## Fonts

Oswald, Bebas Neue, Bungee and VT323 (SIL Open Font License); Luckiest Guy, Special Elite, Permanent
Marker and Schoolbell (Apache License 2.0); Material Symbols (Apache License 2.0), all from
[Google Fonts](https://fonts.google.com).

## Sounds

From Pixabay (Pixabay Content License): "Metal Door Slam" by SoundReality and "Jump-Scare_2" by
freesound_community. Every other sound was synthesized for this project.

## Voices

The story's voices are synthesized locally with [Kokoro](https://huggingface.co/hexgrad/Kokoro-82M)
(Apache License 2.0) through `kokoro-onnx` (`tools/generate_story_audio.py`).
