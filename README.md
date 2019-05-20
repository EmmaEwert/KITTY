# KITTY Imports Tiled Tilemaps Yay

Yet another [Tiled] importer for Unity.

KITTY differentiates itself through seamless basic integration, automatic object-to-prefab
instantiation, and friction-free translation from Tiled custom properties to C# fields, properties
and even methods.

KITTY imports every graphic, object and setting you've defined in your Tiled tilemaps into Unity.

Unity's built-in tilemap editor is okay, but Tiled is *way* better. You can define stuff like text,
warps, and pickups directly in Tiled. KITTY just imports and applies all that seamlessly.

KITTY can't make custom behaviours without coding those behaviours, though. You still need to write
character controllers, enemy AI, interaction behaviours etc. yourself, *or* use an off-the-shelf
engine which will work as expected with KITTY's `PropertyHook` component.

## Seamless basic integration

KITTY supports importing Tiled's `.tmx` and `.tsx` file formats natively in Unity.

The importers are extremely fast, and automatically reimport when you change a tilemap or tileset
outside of Unity.

Advanced Tiled features like collision shapes, per-frame animation framerate and tile objects
*just work*.

## Automatic object-to-prefab instantiation

KITTY aggressively instantiates prefabs from Tiled objects based on the Type property in Tiled.

The suggested workflow is to make a prefab (or prefab variant) for each generic object or tile
object type, attach a bunch of components, and let the custom properties differentiate the specific
object instances.

Alternatively, you can mix and match manually created Unity objects with automatically imported
Tiled objects without losing your work.

## Friction-free translation from Tiled custom properties

KITTY takes care of full Tiled custom properties integration in your game.

The preferred approach to making your game aware of custom properties is to decorate relevant
fields, properties or methods with the `[TiledProperty]` attribute – this automatically assigns the
value defined in Tiled to your C# field or property, or calls your C# method with the property
value.

KITTY automatically figures out the mapping from Tiled to C# based on the name of the C# field,
property or method (case-insensitive). If you need direct control over the mapping, the
`TiledProperty` attribute takes an optional `name` parameter for specifying the Tiled property name.

```csharp
public class Sign : MonoBehaviour {
	[TiledProperty] private string text;
	[TiledProperty("Text Speed")] private float speed;

	[SerializeField] private BorderType border;
	[TiledProperty] private string Border {
		get => border.ToString();
		set => border = Enum.Parse(typeof(BorderType), value, ignoreCase: true);
	}
	private enum BorderType { Wood, Metal, }

	[SerializeField] private Color color;
	[TiledProperty]
	private void Color(string value) => ColorUtility.TryParseHtmlString(value, out color);
}
```

*Note: `[TiledProperty]` is currently only implemented for fields.

KITTY also comes with the `PropertyHook` component for hooking Tiled properties to existing
components' fields, properties, or methods which don't use the `[TiledProperty]` attribute, without
having to code a wrapper.

With this component on a prefab, Tiled properties can define, say, a camera's field of view, the
volume of a background music track, or a GameObject's tag – all imported through Tiled objects or
tiles, and without any additional code.

[Tiled]: https://www.mapeditor.org/