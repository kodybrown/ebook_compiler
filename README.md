ebook_compiler
==============

This is a simple utility that converts .md (markdown) files to html. It then combines them into one ebook .html file.

The real benefit of this is that I can separate my book into separate files. As many or as few as I want. I currently have split up my little book into chapters and sub-sections; one file each.

This little utility will _only_ convert md files that have changed, into html. Once all html files are up to date, they are combined into a single file and wrapped inside the template.

It supports a config (_ebook_config.txt) and a template (_ebook_template.txt) file.

**___ebook___config.txt:**

    title=My book title
    ext=*.md

The template file is really just an html file. Use `@SPLIT@` to indicate in the template where the book contents will be. See the examples folder in the code for details.


**This is not a markdown to html compiler**.
It relies upon [markdown.pl](http://daringfireball.net/projects/markdown/) (a Perl script) available from daring fireball for the actual conversion.


It is definitely a work in progress, but it does what I need for the book I'm working on. And, it is 100 times faster than editing a single md file and converting the whole thing every time.
