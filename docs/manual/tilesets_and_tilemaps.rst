.. _tilesets_and_tilemaps:

Tilesets and Tilemaps
=====================

Tiled is a piece of software that allows you to define tilesets and tilemaps.

KITTY imports those tilesets and tilemaps into Unity, using the native ScriptedImporter class.


Files
-----

A Tiled tileset is defined in a ``.tsx``-file. For regular, grid-based tilesets, a ``.tsx``-file has
exactly one associated image. For image collection tilesets, every tile has a separate associated
image.

Tiled tilemaps are defined in ``.tmx``-files, and reference one or multiple ``.tsx``-files for the
tiles in the tilemap.

For ease of import and use, it is suggested to create images and their related tilesets and tilemaps
in the same folder, somewhere inside your project's Assets folder.

KITTY is entirely non-destructive, so keeping the source images, tilesets and tilemaps somewhere in
the assets folder is not a risk.


Automatic reloading
-------------------

KITTY automatically loads any changed image, tileset or tilemap, and any asset depending on the
changed file.

This means that when you edit images, configure tilesets or draw on tilemaps, Unity will pick up
and apply the changes immediately.

Note that for tilesets, Tiled recommends avoiding a change in width – ie. how many tiles are in a
row. As such, you should add new tile rows to the bottom of the tileset image, if necessary.