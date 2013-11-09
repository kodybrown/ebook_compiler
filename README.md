ebook_compiler
==============

This is a simple utility that converts .md (markdown) files to html. It then combines them into one ebook .html file.

The real benefit of this is that I can separate my ebook into separate files. As many or as few as I want. I currently have split up my little ebook into chapters and sub-sections; one file each.

This little utility will _only_ convert md files that have changed, into html. Once all html files are up to date, they are combined into a single file and wrapped inside the template.

## Config file

It supports a config file for specifying compile settings and a template file. Here is an example config file.

*_ebook_compiler.config*

        title=My ebook title
        ext=*.md
        toc=5_toc.html
        zip=_ebook.zip
        resource_folder=resources
        resource=*.png
        output_filenames=true
        template=_ebook_template.txt
        markdown_compiler=C:\YourPath\Markdown.bat


### Config options

* **title** - The title is only used for the output file name.

* **ext** - The file pattern to indicate what file extensions should be collected up and merged into the ebook. The files are imported based on the order they would show up sorted in your file system.. with one caveat... It uses natural sorting. For instance, normal sorting of files is ordinal-based: `1-file`, `10-file`, `2-file`, etc. Natural sorting does this instead: `1-file`, `2-file`, `10-file`.

* **toc** - The file that represents the table of contents of the ebook. Extra handling is done with the table of contents.

* **zip** - The location of the resources to include into .

* **resource_folder** - The location of the resources to include into the zip file.

* **resource** - The file pattern for resources to include into the zip file.

* **output_filenames** - When this is enabled, entire .md files are wrapped within a DIV element with its title attribute set to the name of the file. I use this while writing, so that when I'm reviewing content I can easily find where it came from.

* **template** - The template file name (see below for details).

* **markdown_compiler** - The file name of the compiler. I use a batch file named `markdown.bat` which is included in the project. It wraps the [markdown.pl](http://daringfireball.net/projects/markdown/) Perl script. The ebook_compiler will call the `markdown_compiler` with two arguments; the in and out file.


## Template file

The template file is really just an html file. Use `@SPLIT@` to indicate in the template where the ebook contents will be. See the following for an example template. This is really just to provide some basic styles and overhead for your ebook.

*_ebook_template.html*

        <html>
        <head>
          <title>My ebook title</title>
          <style media="all">
            * {
              color: #333;
            }

            body {
              /*line-height: 1.2em;*/
            }

            p {
              display: block;
              margin-top: 1em;
              margin-bottom: 1em;
            }

            /* ... */

            pre {
              background-color: #f0f0f0;
            }
          </style>
        </head>
        <body>
          @SPLIT@
        </body>
        </html>


## Dependencies

* **Ionic.Zip.dll** - This allows the whole book (and resources) to be zipped up and made ready for uploading to Amazon. I use version 1.9.1.8. It is in the `dll` folder of the project.


## Summary

It is definitely a work in progress, but it does what I need for the ebook I'm working on. And, it is 100 times faster than editing a single md file and converting the whole thing every time.


### This is NOT a markdown to html compiler

It relies upon some other markdown to html converter. I use a simple batch file that wraps the [markdown.pl](http://daringfireball.net/projects/markdown/) (a Perl script) available from daring fireball for the actual conversion.


