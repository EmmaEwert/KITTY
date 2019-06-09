.. _properties:

Custom properties
=================

Tiled allows you to define custom properties for nearly everything, from properties of entire maps
and layers to objects and tileset tiles.

These custom properties have no effect by themselves. However, they can easily be put to good use
with KITTY.


TiledProperty attribute
-----------------------

To make KITTY import a custom property to a specific field in a script, you simply decorate that
field with the ``[TiledProperty]`` attribute; for example like this:

.. code-block:: c#
	:caption: Enemy.cs

	public class Enemy : MonoBehaviour {
		[TiledProperty] public int damage;
		[TiledProperty] public float speed;
	}

This will read the **Damage** and **Speed** custom properties (case-insensitive, ignoring
whitespace) from the tile, object, layer, or map, and assign the values defined in Tiled to the
respective fields in the C# script.

The ``[TiledProperty]`` attribute can take an optional ``name`` parameter, in case the property
defined in Tiled does not have (roughly) the same name as the corresponding field in C#:

.. code-block:: c#
	:caption: Overriding the Tiled property name

	[TiledProperty("Wait")] public float delay;

This will read the **Wait** custom property from the tile or object, and assign its value defined in
Tiled to the ``delay`` field in the C# script.


Tile properties
---------------

Each tile with a defined **Type** will instantiate the most relevant prefab from anywhere in the
``Assets`` folder named after that type. This is described in more detail in the :ref:`prefabs`
section.

If a field on a ``MonoBehaviour`` attached to that prefab (or any children) is decorated with the
``[TiledProperty]`` attribute, the value of that field is set based on the tile's Custom Property of
the same name – case-insensitive, ignoring whitespace.


Object properties
-----------------

Like tiles, objects with a defined **Type** will instantiate a prefab named after that type. Objects
based on tileset tiles are called tile objects.

Unless a tile object explicitly defines a specific property, that property's value, if any, is
inherited from the source tile's property of the same name.


Map and layer properties
------------------------

For maps and layers, KITTY will instantiate prefabs based on map and layer names, as described in
:ref:`map_and_layer_prefabs`.

Fields declared with the ``[TiledProperty]`` attribute in ``MonoBehaviour``\ s attached to those
prefabs (or any children) will have their values set based on the map or layer's Custom Property
with the same name as that field – case-insensitive, ignoring whitespace.