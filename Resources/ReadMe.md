Images, layout descriptions, binary blobs and string dictionaries are included						<br/>
in this application as resource files.  Various Android APIs are designed to						<br/>
operate on the resource IDs instead of dealing with images, strings or binary blobs					<br/>
directly.

For example, our [main](layout/main.xml) user interface layout, localized [strings](values/strings.xml) table,	<br/>
and [splash logo](drawable/splash_logo.png)	are organized under this Resources directory as follows:

```
Resources/
    drawable/
        splash_logo.png

    layout/
        main.xml

    values/
        strings.xml
```
<br/>

To get the build system to recognize Android resources, set the build action to "AndroidResource".	<br/>
Native Android APIs don't operate directly with filenames, but instead operate on resource IDs.		<br/>
When this app is compiled, the build system packages the resources for distribution					<br/>
and generates a class called "R" (Android convention) containing the tokens for each resource.		<br/>
For example, for the above Resources layout, this is what the R class would expose:

```
public class R {
    public class drawable {
        public const int splash_logo = 0x123;
    }

    public class layout {
        public const int main = 0x456;
    }

    public class strings {
        public const int app_name = 0xabc;
        public const int backup_title = 0xbcd;
    }
}
```

Then we use `R.drawable.splash_logo` to reference the `drawable/splash_logo.png` file,				<br/>
or `R.layout.main` to reference the `layout/main.xml` file, or `R.strings.app_name`					<br/>
to reference the localized app name in the `values/strings.xml` dictionary file.