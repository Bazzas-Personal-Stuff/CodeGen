# CodeGen

A very simple code generator with a similar syntax to JetBrains products' code templates.

## Embedding in a Package

1. Copy this repo to a folder called "CodeGen" inside your package folder: `Packages/com.bazzagibbs.examplepackage/CodeGen`
2. Find and replace the following within the CodeGen folder:
   - "MY_PACKAGE_NAMESPACE", e.g. `BazzaGibbs.ExamplePackage`
   - "MY_PACKAGE_URI", e.g. `com.bazzagibbs.examplepackage`
   - "MY_WIZARD_MENU_ITEM", the submenu under "Tools" where the wizard will be launched from. 
   e.g. `Example Package/Create Custom Example`
       - Note: if you want your wizard to be launched from somewhere other than the "Tools" tab, instead replace "Tools/MY_WIZARD_MENU_ITEM" with the full menu path.
   - "MY_WIZARD_WINDOW_TITLE", the name displayed when the wizard is opened
3. In `CodeGenWizard.cs`, edit the "WizardHelp" string at the top to add a description of the macros in your templates.

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
- **Macro definitions** - What each \$MACRO\$ in the template files should be replaced with. The $ signs should be omitted here.
