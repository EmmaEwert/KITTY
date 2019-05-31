.. _tutorial:

Getting Started
===============

Thank you for using KITTY!

This tutorial takes you through making a small top-down game from scratch, using Tiled to make your
your game. KITTY imports it into Unity, where you can add game-specific behaviours to tiles and
objects from Tiled.

The end result is a grid-based, animated character controller able to interact with signs, NPCs,
and doors which are all defined in Tiled. It plays like this:

.. figure:: images/tutorial-teaser.gif

Keep in mind that this is just a quick example project to get you started; KITTY can do much, much
more than what this tutorial teaches.

Learning how to use Tiled is not part of this tutorial. Have a look at the `Official Tiled
Documentation<https://docs.mapeditor.org/>`_ for that.



Images, Tilesets and Tilemaps
-----------------------------

To begin making a tile-based game, you need to make or find neat tileset images with the same tile
size. These determine what tiles you can build your map with, and to some extent what objects you
can make.

You also need some character spritesheets, which are effectively also a form of tileset, but since
we'll be using them for objects rather than tiles, they don't have to have the same tile size as
your tilesets.

I went with tilesets and spritesheets from Pokémon FireRed/LeafGreen, modified to fit into a grid.
Of course, if you plan to distribute your game, you can't use tiles or sprites you don't have a
license to.


Tileset Images
``````````````

Place the tileset images somewhere in the Assets folder, like in `Assets/Maps/Tutorial`. Ideally,
the Tiled tilesets and Tilemaps you're going to create will end up here, as well. Keeping
map-related images, tilesets, and tilemaps together makes it easier to maintain references and
update the files as you make your game.

Make sure to change the Texture Type in the image import settings to **Sprite (2D and UI)** – this
takes care of proper scaling, and is the most fitting texture type for tiles and sprites.

.. figure:: images/tutorial-image-import.png

If you're making a pixel art game, you also want to set the Filter Mode to **Point (no filter)**,
and Compression to **None**. This keeps your pixels crisp.

Tilesets
````````

Create a new Tileset in Tiled for each of your tileset images, and save them in
`Assets/Maps/Tutorial` as well. Tilesets get the extension `.tmx`, so don't worry about naming them
differently from the source image's filename.

Make sure to set the **Tile width** and **Tile height** to match the tileset images' tile size. If
your tileset's tiles have borders around them, you can set the margin and spacing accordingly.

.. figure:: images/tutorial-new-tileset.png

If you haven't worked with Tiled before, I recommend looking into
`Using the Terrain Brush<https://docs.mapeditor.org/en/stable/manual/using-the-terrain-tool/>`_ in
the official documentation, but don't sweat it.

Feel free to define animations for any animated tiles in your tileset, as well. These will carry
over to Unity with no extra setup.

Tilemaps
````````

Now that you have some tilesets, it's time to make a tilemap!

Anything goes, really. You don't have to worry about interactable stuff like signs or NPCs just
yet – we'll get to those a bit further down. Feel free to add more Tile Layers if you need them.

I made a slightly changed Pewter City from Pokémon FireRed/LeafGreen:

.. figure:: images/tutorial-pewter-city.gif

Note that I didn't bother adding signs yet, and I left out some doors. I will add those to an object
layer later – that way I can directly define the sign texts and door destinations, respectively.


Initial Unity Import
--------------------

Go ahead and drop the entire KITTY folder into the root of your Assets folder.

Did you save your tilesets and tilemaps next to your tileset images? If so, the folder contents
should now look a bit like this:

.. figure:: images/tutorial-folder0.png

The colourful Tiled icons are tilesets, and the tilemap has been made into a prefab. You can drop
that directly into the Hierarchy to see your work in the Scene View.

Whenever you update your tilesets or tilemaps, or edit your tileset images, they automatically get
reimported.

If you go into Play Mode now, any animated tiles will animate, but nothing else really happens.
We're going to change that!


Player Object
-------------

Of course, there are no objects yet – not even a Player. Let's add a small character from a
spritesheet, ideally with a few walking animation frames for each of the four directions. I went
with Leaf from Pokémon FireRed/LeafGreen.

Character and object spritesheets don't need to have the same tile size as the tilemap, as they're
not part of the grid. Leaf's spritesheet, for example, uses 16×32 pixel "tiles" for each animation
frame.

We can insert "tiles" of any size anywhere in the map as objects by adding an Object Layer. I called
my layer "Characters", added a Tile Object of Leaf from the character spritesheet, and set the
object's name to "Leaf". You don't have to give your objects names, but since they carry over to
Unity, it will be easier to tell them apart if you do.

.. figure:: images/tutorial-leaf-object.png

So far, so good. When you switch to Unity now, you'll see your character gets created as a
GameObject with the name you specified, followed by an object ID. A SpriteRenderer child has
automatically been added, and the GameObject even a small name label.

.. figure:: images/tutorial-leaf-gameobject.png

That's all well and good, but the player doesn't do anything, and adding every component manually to
every object that needs any will get tedious quickly.


Player Prefab
`````````````

KITTY automatically generates a SpriteRenderer for us, and if your character "tile" already has an
animation defined, the Renderer child will have a fully configured Animator component as well. You
could even go so far as to add collision shapes to your character "tile", which would generate a
PolygonCollider2D for each shape, but you won't need to do that for your character in this tutorial.

The ability to control how Tiled objects are translated to GameObjects is the primary feature of
KITTY, however!

Let's have the Camera on the Player GameObject instead of at the root of the scene.

Start by removing the Main Camera GameObject from the scene. This will make the Game View complain
about a missing Camera.

Add an empty GameObject to the scene; this will become our Player prefab. Drag it from the scene
Hierarchy to the Project view to save it as a prefab asset – anywhere in the Assets folder is fine,
but let's drag it into `Assets/Maps/Tutorial` for now. It's important to name it "Player" or
something similar, because KITTY uses prefab names to translate from Tiled objects to GameObjects.

Now that you have your empty Player prefab in your Assets folder, go ahead and delete the instance
from the scene, then double click the prefab to enter Prefab Edit Mode.

Add an empty child GameObject named "Camera" to the prefab, and set its position to (0.5, 0.5, -10);
every tile and object imported from Tiled is created at its bottom left position, so to center the
Camera child on the Player character, it needs to be offset by half the width of a "tile" in your
spritesheet. The `-10` Z-position is just to make sure the Camera doesn't near-clip the tilemap and
all the objects.

Finally for now, add a Camera component to the new Camera child, and set its Projection to
Orthographic.

.. figure:: images/tutorial-camera-inspector.png

We'll return to the Player prefab to add more functionality later!

If you want objects based on your new prefab to still have a label, you can choose a label in the
icon dropdown of your root Player GameObject in the top left corner of the inspector.


Typed Objects
`````````````

To let KITTY know that the character you added to the "Characters" object layer in Tiled should use
your new Player prefab for instantiation, all you need to do is set the "Type" property of the
object in Tiled.

.. figure:: images/tutorial-player-object.png

Switching back to Unity, your Game View now shows the "game" with your character in the center.

This approach – creating a named prefab (or prefab variant) and setting the "Type" property of an
object or even a tile in Tiled – is the core way of defining the specific behaviours of your game.


Movement Script
---------------

Not yet written.

Adding Behaviours to Objects
````````````````````````````

Grid Movement
`````````````

Continuous Movement
````````````````````


Colliders and Collision
-----------------------

Colliders
`````````

Collision
`````````


Occlusion with Tile Masks
-------------------------


Interactions
------------

Custom Properties
`````````````````

Simple Sign
```````````

Directional "Sign"
``````````````````


Animating the Player
--------------------

Leaf from Pokémon FireRed/LeafGreen has three walking frames for each of the four
directions, but her actual animation uses the middle frame twice:

.. figure:: images/tutorial-leaf.gif

Facing
``````

Animation
`````````


Advanced: Opening doors
-----------------------

Warp to Scene
`````````````

Animation
`````````

Taking Control of the Player Character
``````````````````````````````````````


Going Forward
-------------

This Tutorial
`````````````

Other Features
``````````````

KITTY Examples
``````````````

To be continued…