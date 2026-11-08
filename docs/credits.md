# Credits

Night Shift is a university project (Machine Learning course, Universidad de Cundinamarca). Third-party
assets are used under the licenses below.

## Pockets

Pockets (his head, body, mallet and textures) is sculpted from nothing by `tools/sculpt_pockets.py`: no
third-party model is part of him. The dummies and spare heads around the circus are a light version of the
same sculpt. Rigged and animated with Adobe Mixamo. His textures use scanned surfaces from Poly Haven
(CC0, see below) for the weave of cloth and the wear of metal.

## 3D models (Sketchfab)

Downloaded from Sketchfab and prepared for the game by `tools/prepare_models.py` (rescaled to meters,
turned to face +Z, moving parts split into their own nodes, textures resized). The prepared files are in
`unity/Assets/Art/Sketchfab/`.

| Model | Author | License | Used as |
| --- | --- | --- | --- |
| [Kiri '86 - Low poly model](https://sketchfab.com/3d-models/kiri-86-low-poly-model-5c17c582ef9549798f694b660feea42a) | Daniel Zhabotinsky | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) | The guard's car, without its badges (`car_wagon.glb`) |
| [Destroyed Car 03 / Backrooms Car (Gameready ver)](https://sketchfab.com/3d-models/destroyed-car-03-backrooms-car-gameready-ver-efc4dfe2c7284a64bc9281eb4f61d402) | Renafox (@kryik1023) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) | The wreck in the back lot (`car_wreck.glb`) |
| [Industrial Electricity's Lever](https://sketchfab.com/3d-models/66915915e3234d319415d6bfb4e3a59e) | Amine.Elouneg | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) | The exit lever (`exit_lever.glb`) |
| [Early 1900s Knife Switches Type C](https://sketchfab.com/3d-models/033dd717e4b04e46b6da85195085b8f3) | Mad_Lobster_Workshop | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) | The breaker box's main switch (`knife_switch.glb`) |

## Props and surfaces

The props (`tools/fetch_models.py`) and the scanned surfaces (`tools/fetch_surfaces.py`) come from
[Poly Haven](https://polyhaven.com) and [ambientCG](https://ambientcg.com) under
[CC0](https://creativecommons.org/publicdomain/zero/1.0/). The striped circus canvas is printed and aged
over Poly Haven's "Rough Linen" by `tools/fetch_surfaces.py`.

## Posters

The wall posters (`tools/generate_posters.py`) use details of real lithographs from around 1890-1900 on
Wikimedia Commons, cropped so no show's name remains and set under new type. All are in the public domain
or marked with no known copyright restrictions:

- [5 circus clowns](https://commons.wikimedia.org/wiki/File:5_circus_clowns_LCCN2002718921.jpg)
- [Clown detail, The marvelous foot-ball dogs](https://commons.wikimedia.org/wiki/File:Clown_detail,_The_marvelous_foot-ball_dogs,_poster_for_Barnum_%26_Bailey,_1900_(cropped).jpg)
- [Clown art detail, clowns and geese](https://commons.wikimedia.org/wiki/File:Clown_art_detail,_Barnum_%26_Bailey_clowns_and_geese_(cropped).jpg)
- [Cock fighting and clown detail](https://commons.wikimedia.org/wiki/File:Cock_fighting_and_clown_detail_from_Barnum_%26_Bailey_poster_(cropped).jpg)
- [Twenty funny felt-crowned fools](https://commons.wikimedia.org/wiki/File:Twenty_funny_felt-crowned_fools,_poster_for_Forepaugh_%26_Sells_Brothers,_1899.jpg)
- [The famous Jandaschewsky family of clowns and entertainers](https://commons.wikimedia.org/wiki/File:The_famous_Jandaschewsky_family_of_clowns_and_entertainers_(5463615958).jpg)

## Fonts

Oswald, Bebas Neue, Bungee and VT323 (SIL Open Font License); Luckiest Guy, Special Elite, Permanent
Marker and Schoolbell (Apache License 2.0); Material Symbols (Apache License 2.0), all from
[Google Fonts](https://fonts.google.com).

The signs and the title also use Rye, and the interface uses Oswald (both SIL Open Font License).

## Sounds

From Pixabay (Pixabay Content License): "Metal Door Slam" by SoundReality and "Jump-Scare_2" by
freesound_community.

Freddy's sounds (CC0, converted to mono, trimmed and normalized in `Assets/Audio/Freddy/`):

- Steps: "Metal footsteps on concrete" by Thimras, OpenGameArt
  (https://opengameart.org/content/metal-footsteps-on-concrete).
- Servos: "Servo 1–5" from the "Electromechanical" pack by JoontheFloof, Freesound
  (https://freesound.org/people/JoontheFloof/sounds/740238/, 740240, 740241, 740242, 740243).
- Creaks: "Metal Creak 1–4" by ingudios (https://freesound.org/people/ingudios/sounds/119465/,
  119466, 119467, 119468) and "Long Metal Creak" by LoafDV
  (https://freesound.org/people/LoafDV/sounds/238289/), Freesound.

The clown's sounds (CC0, cut into one-shots by `tools/prepare_clown_sounds.py`, in `Assets/Audio/Clown/`),
all from Freesound:

- Squeaky toy: "Squeaky Toy #1" by Breviceps (https://freesound.org/people/Breviceps/sounds/468443/) and
  "squeak-toy-squeeze" by Aiyumi (https://freesound.org/people/Aiyumi/sounds/245083/).
- Surprise box: "Jack in the box" by rschiwal (https://freesound.org/people/rschiwal/sounds/63980/) and
  "Boing Box 01" by D.jones (https://freesound.org/people/D.jones/sounds/527779/).
- Shoes: "sneaker squeak rubber" by shakaharu (https://freesound.org/people/shakaharu/sounds/88502/).
- Bells: "Jingle-Bell" by romulofs (https://freesound.org/people/romulofs/sounds/127553/) and
  "Jingle Bells" by JarredGibb (https://freesound.org/people/JarredGibb/sounds/263886/).
- Music box: "creepy music box" by Modification1089 (https://freesound.org/people/Modification1089/sounds/274379/).

The home screen's music is "Entrance of the Gladiators" (Julius Fučík, 1897), played by the United
States Marine Band, public domain
([Wikimedia Commons](https://commons.wikimedia.org/wiki/File:Julius_Fu%C4%8D%C3%ADk%27s_%22Entrance_of_the_Gladiators%22,_performed_by_the_U.S._Marine_Band.oga)),
slowed and aged by `tools/generate_menu_music.py`.

Every other sound was synthesized for this project. Recordings dropped into
`Assets/Audio/Clown/{Squeak,Pop,Shoes,Bells}` (then Night Shift > Update Game Assets) are used for the
clown's toys, boxes, shoes and bells.

## Voices

The story's voices are synthesized locally with [Kokoro](https://huggingface.co/hexgrad/Kokoro-82M)
(Apache License 2.0) through `kokoro-onnx` (`tools/generate_story_audio.py`). Pockets the clown is the
`bm_fable` voice, cut phrase by phrase and pushed around in pitch and speed to sound unhinged, through an old public-address effect, and can be replaced by recording over the same file names.
