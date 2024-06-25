
<h1 align="center">
<img src="https://github.com/realChrisDeBon/JsonAot/assets/97779307/15598c4d-eab5-414f-b349-c1010f610111" width="175" height="175" alt="ConsoleDebugger logo">
  
   JsonAot
</h1>

## Json serialization solution for assembled-on-time publishing in .NET 8.0

JsonAot is a lightweight utility designed to automate source generation code for your classes.
If you are trying to refactor or migrate your code into an AOT compliant format, you may find
several Json serialization packages are not entirely AOT compliant and will break code, which
will lead you to consider using `System.Text.Json`. Rather than deal with the headache of 
implementing a huge deal of refactoring, you can simply add the attribute `[JsonAot]` to your
classes that you intend to serialize or deserialize and JsonAot will handle the rest. Here's
how:

**1. Run JsonAot -setup** Running JsonAot with the -setup parameter in your project's
root directory will create the JSONHandler.cs template file. Optionally you can add the
-s arg during setup, `JsonAot -setup -s' and your 'Program.cs' file will be automatically
updated with the necessary including statement.

*Note:* Manually add your project's namespace into the JSONHandler.cs file so it can access class references!

**2. Implement functions** JSONHandler.cs has two built in functions which provide a very
familiar syntax with Newtonsoft's and System.Text.Json's conversion syntax. Implement these
function for your project's serialization and deserialization, and be sure to tag all classes
that will be passed to these function with `[JsonAot]`
```csharp
[JsonAot]
public class MessageExample{
    public string Message {get; set; };
    public int ID {get; set;}
} // the attribute will tell JsonAot this class will be serialized
```

**3. Run JsonAot -run** Once all of your classes have been properly tagged with the JsonAot
attribute, run `JsonAot -run` in the project root directory. A scan will be conducted of all
*.cs project files, the proper classes identified, and JSONHandler.cs will be updated automatically with appropriate source code.

And there you go, your Json serialization will work within the confines of an AOT binary!

## Function usage
If there are no project-specific constraints, it is advised to use the following includes:

```csharp
global using static JSONHandler.JSONHandler;
global using JSONHandler;
```

This will make conversion much more functional and easy to implement throughout your application.
See below for examples of the built-in serialization functions.

**Serialization:**
```csharp
MessageExample newmessage = new MessageExample(){
    Message = "Hello!",
    ID = 1000
};
string serialized_object = Serialize<MessageExample>(newmessage);
```

**Deserialization:**
```csharp
MessageExample deserializedmessage = Deserialize<MessageExample>(serialized_object);
```

*Note:* JsonAot is meant to be ran as a binary in the project root, it is not a working NuGet package. Download and compile the binary, or check the release for a current working download.

<sup>If you don't want to compile it yourself, there's a working x64 release</sup>
