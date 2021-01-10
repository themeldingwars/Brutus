# Brutus

A SDB table & field name cracker.



Usage: brutus *[ command ] < params >*

|Command|Description                                                                                                 |
|-------|------------------------------------------------------------------------------------------------------------|
|attack |Generates a brutus result from provided databases.                                                          |
|export |Performs either a Binary or Dictionary attack on provided files and appends matches to a brutus result file.|
|test   |Exports the "best guesses" from a brutus result to a flat dictionary file.                                  |
|test   |Tests a single hash against a list of strings.                                                              |

In the root directory of the repo we have provided a brutus result file (brutus.json)\
This result was generated from the database of 208 different builds and should cover most of the names in any sdb.

**While the generated results can be very helpful, they need to be taken with a grain of salt due to the nature of the hash
used to obfuscate them in the first place. Some of these hashes have multiple matches or might be completely wrong.**

*This project was created for educational purposes only and should not be used by anyone.*