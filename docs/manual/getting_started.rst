.. _getting_started:

Getting Started
===============

Find or make some tileset images. You'll be using these to build your maps. Save them somewhere in
the Unity ``Assets`` folder, preferably somewhere like ``Assets/Maps/Tileset.png``.

In `Tiled <https://www.mapeditor.org/>`_, make a few tilesets and tilemaps using those tileset
images, and save them next to the tileset images. You can put the tilemaps in subfolders, like
``Assets/Maps/World 1-1/Tilemap.tmx``, etc.

In Tiled, make every interactive part of your tilemaps (:guilabel:`Player`, :guilabel:`Sign`\ s,
:guilabel:`NPC`\ s, :guilabel:`Coin`\ s, etc.) into a Tiled object, and give the objects separate
Types based on their, well, type.

In Unity, make a prefab for each of the Types you used in Tiled. You can add as many built-in and
custom components as you want.

Every time you reimport the tilesets or tilemaps, those prefabs get instantiated for each Typed
object and tile.

You can use ``[TiledProperty]`` attributes to translate a Custom Property from Tiled into a regular
field in your custom ``MonoBehaviour``\ s.