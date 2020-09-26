# stitch-IL

**A program made for the purpose of modifying assembly CIL code, using Cecil.**

You can make a patch file and the program will apply the patch to the target file.
You only have to write the code, then specify (with attributes) where to insert your code. And this program will do the rest for you.

For more info see documentation: https://github.com/gvk/stitch-IL/blob/master/Documentation.md

Build download: https://github.com/gvk/stitch-IL/releases/tag/v0.2-alpha  

Start the program with: `stitch-IL.exe fabric.dll patch.dll`  
Where "fabric" is the target and "patch" is your code you want to "stitch together".  
<br />
<br />
_( This entire program was written hastily, without much planning nor thinking. You may experience bugs and issues. Maybe even a headache or two while reading the code. )_  
_( This program was not made when it was pushed to github, so libraries used might be outdated by several years. )_  
_( I might return to fix this some other time, else feel free to fix it yourself. )_  
<br />
<br />
Cecil: https://github.com/jbevain/cecil
