# Documentation

Before I begin losely explaining how to use this tool. Let's just look an an example:
```
public class GameState_MainPatch {
  [PatchInformation(Offset = 137)]
  public static void onPlayUpdate() {
      if (Input.GetKeyDown(key)) {
        Console.WriteLine("Key pressed!");
      }
    }
  }
```
Here we write a class in a separate dll file that will patch another file.  
The class we patch is "GameState_Main" and we mark our code containing patches with the suffix "Patch".
Then name our method the exact same as the one we want to patch.
The attribute tells stitch-IL to insert a method call to this method in the method GameState_Main::onPlayUpdate on IL instruction 137.
So once patched, and we run the patch program, it will print "Key pressed!" if it reaches that code.  
<br />
## Why the name?
You can think of it as stitching a "patch" on a "fabric", it will insert "stitches" connecting the "fabric" to the "patch".  
<br />

## How to use
You may edit stitch-IL to support your needs and fix issues with it, but here is how it works as of writing this:  
1. Make a dll file with your changes.
2. Start the program with: `stitch-IL.exe fabric.dll patch.dll`
3. It will output a modified fabric.out.dll

##H ow to write patches
If might be worth looking at the source code, to understand a little bit better. Either way, here is my explanation:
1. Find the classes / types you want to edit.
2. Find the method you want to edit in that class.
3. In a separate project write a class named `[Target Class Name]Patch` - I mean, that is suffixed with "Patch"
4. In the class make a `public static` method/function named the same as the method you like to edit.
5. Add the attribute `PatchInformation` to it, by referencing stitch-IL and using the attribute found in there.
6. Take a look below what properties the `PatchInformation` attribute has, and modify the function arguments/parameters if needed.
7. Write your code in the method.
8. Compile to a library.
9. Any references needed (maybe even the target dll itself) can be put in a folder "references" next to the stitch-IL.exe
10. Run the program

## Properties of the PatchInformation attribute
* IL_Offset: IL instruction offset/index, i.e. where at the target code to place your method call. (default: 0)
* IL_ReturnTo: IL instruction index to return to after patch has been called (and has returned). (default: -1  meaning continue)
* DoReturnAfterCall: whether the method should return after the patch has returned. (default: false)
* DoReturnIfNullReturn: whether method should return if patch returns null. (default: false)
* StoreLoadResult: whether to store and load the result of the patch in a variable (by index) in the target method, it will push the variable afterward so the next IL instruction has use it. (default: -1  meaning don't do this)
* LoadVar: whether to load a variable (by index) before calling the patch, effectively passing the variable to the patch. (default: -1  meaning don't do this)
* PassOnAllArgs: whether to pass on all arguments of the target method to the patch, it will pass an object[] with all arguments. (default: false)
* PassInstanceArg: whether to pass on the instance (aka "this") to the patch. (default: false)
* PassArg_ByIndex: pass a specific argument by its index. So you can say: 2 to get the argument with index 2. if you set it to: `0x100 | 1<<0 | 1<<2 ` will cause it to use a bit map, and so it will load both argument 0 and argument 2. (default: -1  meaning don't pass)
* InsertDupInstruction: inserts a dup instruction before the patch is called (you have to accept that parameter in your function). (default: false)
* DecideReturn: tells it that this method determines whether to return or not (true/false) in the target method. (default: false)
* TargetNumberOfArgs: what number of args the target method has, could be used for filtering a certain one. (default: -1  meaning don't care)

**Note: You have to change your patch method to accept the arguments you choose to pass, else you will get a runtime error.**
Sometimes the stitch-IL might warn you about this.

## Additional features:
Sometimes you want to patch one method multiple times, but it is impossible to have the same method name twice:
Solution: You can add "II" as a suffix to your methods to have multiple method patches:
```
[PatchInformation(Offset = 137)]
public static void onPlayUpdate() {}

[PatchInformation(Offset = 654)]
public static void onPlayUpdateII() {}
```
You can add as many "II" as you like, though it has to be in pairs.  
<br />
Sometimes the types or methods have angle brackets in the name < >. And it wouldn't be possible to have that as a type/method name.
You can therefore use `_LT_` and `_GT_` as a replacement.  
`public static void _LT_Initialize_GT_m__14A()`  
<br />
For the constructors you simply ommit the dot, and write `ctor` and `cctor` respectivly.
<br />
<br />
Only Types with suffix "Patch" will be considered, only methods with the PatchInformation 
