# BreakableWallRandomiser

## Overview

Randomizes some walls in the game hollow knight. Currently randomizes, with a few exceptions:
 - Any wall with FSM breakable_wall_v2
 - Any wall with FSM break_floor
 - Any wall with FSM "FSM"
 - Any floor broken by Dive / Descending Dark in the game

 The exceptions:
 - Any walls in Godhome
 - The two Hunter's Mark one-ways
 - Fungal Wastes to Deepnest wall which had annoying logic implications

Hitting a wall won't result in it breaking; you'll get an item instead. In order to break the wall, you'll need to find its corresponding item. If you find a wall's item but haven't collected its check yet, it will become translucent and you will be able to walk through it, but still hit it. 

### Usage

Install the mod as usual. Ensure you have Randomizer 4 (RandomizerCore) and Satchel (SFCore) installed too.

Report logic errors, or incorrectly blacked-out rooms, on GitHub or on Discord. Mod is currently not fully integrated with some connections' logic.

### Acknowledgements

Thanks to the entire Hollow Knight Modding Discord for their support throughout the development of this project. In particular, I'd like to call out:
 - **BadMagic** for getting me up to speed with rando logic, and various coding help
 - **Flibber** for their coding help and advice with rando stuff (and for coding Lever Rando / RandoPlus so I can take examples from that code :d)
 - **Ender Onryo** for their memes, moral support, and logic labbing
 - **Anyone else who has reported a bug or helped me with the mod**. Thank you all so much!
