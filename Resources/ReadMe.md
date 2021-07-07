Images, layout descriptions, binary blobs and string dictionaries can be included					<br/>
in your application as resource files.  Various Android APIs are designed to						<br/>
operate on the resource IDs instead of dealing with images, strings or binary blobs					<br/>
directly.

For example, a sample Android app that contains a user interface layout (main.xml),					<br/>
an internationalization string table (strings.xml) and some icons (drawable-XXX/icon.png)			<br/>
would keep its resources in the "Resources" directory of the application:

Resources/																							<br/>
    drawable/																						<br/>
        icon.png

    layout/																							<br/>
        main.xml

    values/																							<br/>
        strings.xml

In order to get the build system to recognize Android resources, set the build action to			<br/>
"AndroidResource".  The native Android APIs do not operate directly with filenames, but				<br/>
instead operate on resource IDs.  When you compile an Android application that uses resources,		<br/>
the build system will package the resources for distribution and generate a class called "R"		<br/>
(this is an Android convention) that contains the tokens for each one of the resources				<br/>
included. For example, for the above Resources layout, this is what the R class would expose:

public class R {																					<br/>
    public class drawable {																			<br/>
        public const int icon = 0x123;																<br/>
    }

    public class layout {																			<br/>
        public const int main = 0x456;																<br/>
    }

    public class strings {																			<br/>
        public const int first_string = 0xabc;														<br/>
        public const int second_string = 0xbcd;														<br/>
    }																								<br/>
}

You would then use R.drawable.icon to reference the drawable/icon.png file, or R.layout.main		<br/>
to reference the layout/main.xml file, or R.strings.first_string to reference the first				<br/>
string in the dictionary file values/strings.xml.