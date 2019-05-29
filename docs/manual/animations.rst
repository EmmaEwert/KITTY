.. _animations:

Animations
==========

Tile animations defined in Tiled tilesets work as expected, and support per-frame durations.

By default, objects defined in Tiled tilemaps based on animated tiles also work as expected, looping
through each frame according to its duration.


Custom animation sequences
--------------------------

Sometimes, the frames of an animated tile aren't meant to be played fully in sequence; for example,
a character sprite sheet tileset might have animations for idle, run, and jump, all defined in the
same animation in Tiled.

By setting the "Start" and "End" properties of an object's child Animator component, any subsequence
can be played at any time.

For example, a character controller script with 2 idle frames followed by 3 run frames might contain
the following:

.. code-block:: c#

	void Update() {
		var animator = GetComponentInChildren<Animator>();
		if (running) {
			animator.SetParameter("Start", 2);
			animator.SetParameter("End", 4);
		} else {
			animator.SetParameter("Start", 0);
			animator.SetParameter("End", 1);
		}
	}

This code tells the Animator to cycle through frames 2, 3, and 4 when running. When not running,
frames 0 and 1 are cycled through instead.