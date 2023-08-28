# BlendTreeBuilder

### A Unity tool to make VRC Blendtree creation easier and faster

Currently, this tool is only capable of handling toggle layers for the most part.  
It is not perfect by any means but will hopefully prove useful in optimizing your toggles.

# Installation
1. Download the Unity package from [releases.](https://github.com/Dreadrith/BlendTreeBuilder/releases)
2. Import the Unity package into Unity.

# How to use
1. Open the window by finding it in the toolbar: DreadTools > BlendTreeBuilder
2. Make sure that the FX Controller set is the controller you want to optimize and press Next.
3. Press 'Optimize!' at the bottom.
4. Done!

![ready window](https://github.com/Dreadrith/BlendTreeBuilder/raw/main/media~/wind1.png)

# Details
On the second step, in the optimize tab, you're given details on what will be handled.
- 'Make Duplicate' will make a backup of your controller before proceeding.
- 'Replace' will delete the layer for the toggle that will be optimized.
- 'Active' will determine wether this toggle will be handled or not.
- Yellow warning icon appears if the toggle behaviour may change when optimized, such as with dissolve toggles.
- Red warning icon appears if optimizing this toggle may break some functionality, such as with exclusive toggles through parameter drivers.
- Foldout is to see or change what start and end motions will be used for this toggle.

![optimize window](https://github.com/Dreadrith/BlendTreeBuilder/raw/main/media~/wind2.png)

### Notes
You should almost always make backups in case something doesn't work right.  
After running the tool, you should test whether they work with [this emulator](https://github.com/jellejurre/Av3Emulator/tree/add-parameter-mismatch).  
It's important to use the fork! The original does not support parameter mismatching yet.  
If something doesn't work, you can go back to optimize the original again and disable 'Active' for the toggles that didn't work.

### Warning
The optimizer does not take into account layer priority. If an optimized toggle has overlapping clips with another clip, there may be change in behaviour where properties get overwritten.

## Planned Features
- Handle dedicated motion time layers
- Handle dedicated single blendtree layers
- Implement float smoothing for clip blending
- Make the builder for faster and easier tree building
- Optimize my smooth brain

![tree preview](https://github.com/Dreadrith/BlendTreeBuilder/raw/main/media~/wind3.png)
