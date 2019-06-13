.. _issues:

Known issues
============

KITTY is a work in progress. It has missing features, known issues, and a few known bugs.

If you find an issue or a bug not listed here, you can `contact me
<mailto:emma.o.ewert@gmail.com>`_.


Isometric and hexagonal tilemap objects are misplaced
-----------------------------------------------------

KITTY's initial focus is on orthogonal tilemaps. While isometric and hexagonal tilemaps import and
display just fine, any objects defined within will be offset immensely.


Visible property in Tiled does nothing
--------------------------------------

Ideally, toggling the **Visible** property on a Tiled object should still create a
``SpriteRenderer``, but disable it by default.


Non-power-of-two tileset texture misalignment
---------------------------------------------

KITTY doesn't take into account any difference between a tileset's defined width and height, and a
non-power-of-two texture's Unity-rescaled width and height.

A quick fix is to change the tileset image's import settings to not rescale non-power-of-two
textures.


Private ``[TiledProperty]`` fields aren't serialized
----------------------------------------------------

While it doesn't make sense to import a property value to an unserialized field (it'll just be
lost), the ``[TiledProperty]`` attribute doesn't automatically force field serialization.

A quick fix is to add the ``[SerializeField]`` attribute as well.


Tile collision shape types are ignored
--------------------------------------

Typed tile collision shapes should probably instantiate a prefab per shape. They currently don't.


Template objects don't work
---------------------------

Templates are not yet implemented in KITTY.