# Force Respawn

Allows you to force a respawn (map reload) from the pause menu, which can be useful if the game gets into a bugged state due to some mods or otherwise.

Additionally, it fixes an old rare bug that can occur when you join an online multiplayer session, where all stacks in your inventory are temporarily reduced to 1.

In this state, any items spent from those stacks are duped. But if you disconnect while in this state, all stacks permanently get reduced to 1 on your save.
The bugged state is fixed by reloading the map (by resting, or moving to another map)
This bug seems to be rare, as the last report I could find on Google was from 2020, before Definitive Edition was even released.
But it can still occur on DE, as I have been personally encountering it nearly every time I join a game on one of my characters. It seems to be tied to a specific character.

## What it does

A new menu option is added to the pause menu, which allows you to respawn (reload the map) at will.
The spawn point can be configured from the BepInEx configuration manager.

The respawn function acts similarly to resting, but without affecting your buffs or stats or passing time.

The stack bug fix included in this plugin simply force reloads the map on join.
You will see a longer loading screen when joining a game, since it has to load twice.
The reload happens for all connected players, for that reason I don't recommend enabling it unless you actually encounter this bug.
If you still want to enable it, you can do so in the configuration manager.
