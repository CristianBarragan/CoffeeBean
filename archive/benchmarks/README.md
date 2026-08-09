# benchmarks

No benchmark project existed in the original repo, so nothing was migrated here.
A natural first candidate: a BenchmarkDotNet project comparing Graphgine's SQL
compilation cost against the pre-split `CoffeeBeanery` implementation (removed
from the tree ahead of the first public release — check out a commit before its
removal, or the `graphgine/pre-split` tag if one is cut, for the baseline).
