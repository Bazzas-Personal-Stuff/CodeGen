
# CodeGen

A very simple code generator with a similar syntax to JetBrains products' code templates.

## Embedding in a Package

1. Clone this repo directly into your package folder: `<UnityProj>/Packages/<com.your.package>/CodeGen`.
2. Delete the `.git` directory and `.gitignore` file from the CodeGen directory.
3. Find and replace the following within the CodeGen folder:
   | Find | Replace Example | Description |
   | --- | --- | --- |
   | `MY_PACKAGE_NAMESPACE` | `BazzaGibbs.ExamplePackage` | The namespace of the package you're embedding this into. |
   | `MY_PACKAGE_URI` | `com.bazzagibbs.examplepackage` | The URI of the package you're embedding this into. |
   | `MY_WIZARD_MENU_ITEM` | `Example Package/Create Custom Example` | The submenu under "Tools" where the CodeGen wizard will be launched from. |
   | `MY_WIZARD_WINDOW_TITLE` | `Create Custom Example` | The window title of the wizard when it has been launched. |
   | `MY_WIZARD_HELP_DIALOGUE` | `Descriptions for each macro: ...` | Contents of the information dialogue box at the top of the wizard. |
       
Note: if you want your wizard to be launched from somewhere other than the "Tools" tab, instead replace `Tools/MY_WIZARD_MENU_ITEM` with the full menu path.

4. Delete the example template in `CodeGen/Editor/Code Templates/ExampleTemplate.txt` and replace with your own code templates.

## Writing Templates

```
###Game Variables/Editor/$TYPE.PASCAL$Drawer.cs
using UnityEditor;
namespace $NAMESPACE$ {
    [CustomPropertyDrawer(typeof($TYPE.PASCAL$))]
    class $TYPE.PASCAL$Drawer : PropertyDrawer {}
}
```

1. The first line of a template must begin with three # symbols, immediately followed by a local file path.
This line specifies the destination file path relative to the root output directory.
2. The rest of the file is valid C# code, except for macros which will be replaced.

### Macros

- Valid macros have the form `$MACRO_NAME.MODIFIER$`, where the modifier is optional.
- Modifiers perform some processing on the replacement string. For example, with a macro `$TYPE$` defined as the word "float"
  - `$TYPE$` is replaced with `float`
  - `$TYPE.PASCAL$` is replaced with `Float`
- Valid modifiers include:
    - `UPPER`: MYCLASS
    - `LOWER`: myclass
    - `PASCAL`: MyClass
    - `CAMEL`: myClass
    - `NAME`: same as `PASCAL`, but strips any non-alphanumeric. e.g. `MyClass<float>` -> `MyClassfloat`
- note: Pascal and Camel are currently limited to modifying the first character of a string.

## Unity Editor Wizard

### Buttons

- The "Get Macros" button will scan all the provided template files for valid macros and populate the "Macros" list with
empty entries.
- The "Generate" button will create an output file from each template and place it in the directory specified. If a file with the same name
already exists in the target directory it will be overwritten.

### Parameters

- **Template directory** (private) - Where the code generator will search for template files.
- **Output directory** - The root destination folder for any generated code files. Templates may create directories inside this folder.
- **Macro definitions** - What each `$MACRO$` in the template files should be replaced with. The $ signs should be omitted here.

## CodeGen Repository

The git repository for this project can be found at https://github.com/bazzas-personal-stuff/codegen.
