# C# class to store settings
I write utills and robots from time to time. And everytime need to handle settings.  
50/50 I need to read file with settings to catch new values.
Of course, this is not a complex at all. But sounds good!


So this is class splited in two files: custom part for current project and universal part for all projects.  
+ public fields sets by CMD params, ENV params and finaly by txt file.  
+ unknown keys raise an error - newer miss type
+ double params in CMD and ENV raise error
+ params from CMD and ENV have override values from txt file
+ custom parameters can be made with private fields and property combo
+ read params ones or repeat on every time txt file modifed
+ one problem - if txt file is a symbolic link. Windows links are broken. By design.
+ it is possible to pass params to program by CMD/ENV only. Without file at all
+ made with C# reflection
+ `.toString()` on class outputs all settings in human readble format.

# Key-Value format
In ENV it is native key-value pair.  
On CMD format simplified to `-key=value` or `"-key=value value value"`.  
In txt is `key = val val val`. Space, # or ; hide the line. No line feed allowed.


