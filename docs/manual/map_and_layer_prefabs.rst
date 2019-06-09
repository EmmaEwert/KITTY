.. _map_and_layer_prefabs:

Map and Layer prefabs
=====================

Like :ref:`prefabs` for tiles and objects, KITTY will instantiate the most relevant prefab named
after a map or layer, if it exists.

Relevance is determined by how much of the prefab path matches the tilemap path.


Map components
--------------

A ``Grid`` component, static ``Rigidbody2D`` component, and a ``CompositeCollider2D`` component are
always automatically added, so even with a custom map prefab, you don't need to add those components
yourself.

Components on and children of a custom map prefab stay on the tilemap ``GameObject`` too, of course.

.. Tip:: You can use a custom map prefab for multiple tilemaps by naming it something like
	:guilabel:`Map`, and making named prefab variants of this :guilabel:`Map` prefab original.


Layer components
----------------

For layers, ``Tilemap``, ``TilemapRenderer`` and ``TilemapCollider2D`` components are always
automatically added.

Components on and children of a custom layer prefab stay on the layer ``GameObject`` too, of course.

The same layer prefab can be used for a layer in multiple tilemaps if they have the same layer name.

Since the prefabs are loaded based on relevance, you can have separate prefabs for layers with the
same name by just having each tilemap in a separate folder, along with that tilemap's layer prefabs.