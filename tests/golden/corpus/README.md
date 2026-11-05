# The golden corpus

One file per manifest Ohmline has ever stored or sent: the JSON exactly as that manifest was
first written, frozen (section 20.5). `CorpusTests` in `Ohmline.Node.Tests` reads every file
here with the serializers the node runs today, on every build, for ever.

The golden files one folder up say what each manifest is written as *now*, and change when a
shape gains an optional field (section 20.2). The files here never change.

## Rules

- **Never edit or delete a file here.** A review that finds one changed refuses it. A file here
  that no longer reads is a journal that no longer replays: fix the serializer, never the file.
- **A new manifest gets its file here when it first ships**, copied from its golden file.
  `CorpusTests.Every_manifest_the_node_reads_has_its_first_stored_json_in_the_corpus` fails
  until it does.
- **A new version of a manifest** (`.v2`) gets a file of its own; the old version's file stays,
  and the serializer reads it through an upcaster (section 20.3).
- **Before accepting a golden file that changed** because a field was added, copy the old one
  here if its manifest has no file yet.

The files present when the corpus was started were taken from the tag `ch19-s08`, the last
tag before Chapter 20 changed any stored shape.
