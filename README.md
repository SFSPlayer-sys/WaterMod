# WaterMod

WaterMod adds water to SFS in advance.

You can use it by adding the following code to the planet file:

```json
"WATER_DATA": {
    "ocean mask texture": "Earth_Oceans_2",
    "sand": {"r": 0.9, "g": 0.86, "b": 0.81, "a": 1.0},
    "floor": {"r": 0.25, "g": 0.25, "b": 0.25, "a": 1.0},
    "shallow": {"r": 0.1, "g": 0.68, "b": 1.0, "a": 0.5},
    "deep": {"r": 0.0, "g": 0.1, "b": 0.55, "a": 1.0},
    "sea level height": 75,
    "water density": 250,
    "water drag coefficient": 0.5,
    "water drag multiplier": 1.0
}
```

## Explanation of each parameter

- `ocean mask texture`: The ocean mask texture used. In the mask, white represents oceans and black represents land.
- `sand`: RGBA values of sand.
- `floor`: RGBA values of the seabed.
- `shallow`: RGBA values of shallow water.
- `deep`: RGBA values of deep water.
- `sea level height`: The height of the sea level. Areas below this height are considered underwater.
- `water density`: Density of water.
- `water drag coefficient`: Coefficient of water drag.
- `water drag multiplier`: Multiplier of water drag.

I know the water physics may not be very realistic, but this is the best way to ensure no abnormalities.
