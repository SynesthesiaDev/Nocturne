# 🌙 Nocturne

![GitHub Workflow Status](https://img.shields.io/github/actions/workflow/status/SynesthesiaDev/Nocturne/build.yml?branch=main&style=for-the-badge&label=Build&color=33cc33)
![NuGet Version](https://img.shields.io/github/v/release/SynesthesiaDev/Nocturne?style=for-the-badge&color=blue&label=Release)

![Target .NET](https://img.shields.io/badge/.NET-10.0-512bd4?style=for-the-badge&logo=dotnet)
![License](https://img.shields.io/badge/License-MIT-black?style=for-the-badge)

**Lightweight easy-to-use local database for C#**

No reflection, no hidden native wrapper, no magic

---

### Usage

Create one global database object

```csharp
public static readonly NocturneDatabase NOCTURNE_DATABASE = new NocturneDatabase
{
    FilePath = "./data/database.nocturne",
    CompactOnLaunch = true,
    AutomaticallyCompact = false
};
```

In all of your "models" you then define a collection object:
```csharp

public record Person(string Name, int Age, bool IsCool) 
{
    public static readonly NocturneCollection<string, Person> DB_COLLECTION = Program.NOCTURNE_DATABASE.For(
        collectionKey: "people",
        schemaVersion: 1,
        keySerializer: KeySerializers.STRING,
        valueSerializer: DATABASE_SERIALIZER
    );
}
```

Every model needs their own serializer. Nocturne does no do any source generation or reflection by design so you will need to provide your own serializer.

You can either write one manually:
```csharp
public static readonly INocturneSerializer<Person> DATABASE_SERIALIZER = NocturneSerializer.For<Person>
(
    (buffer) =>
    {
        var name = buffer.ReadString();
        var age = buffer.ReadInt();
        var isCool = buffer.ReadBoolean();
        return new Person(name, age, isCool);
    },
    (buffer, person) =>
    {
        buffer.WriteString(person.Name);
        buffer.WriteInt(person.Age);
        buffer.WriteBoolean(person.IsCool);
    }
);
```

Ore uses an `IBinaryCodec` which is from [Codon](https://github.com/SynesthesiaDev/Codon) library that comes packaged in:

```csharp
public static readonly IBinaryCodec<Person> CODEC = BinaryCodecs.For<Person>()
    .Field(BinaryCodecs.STRING, p => p.Name)
    .Field(BinaryCodecs.INT, p => p.Age)
    .Field(BinaryCodecs.BOOLEAN, p => p.IsCool)
    .Build((name, age, cool) => new Person(name, age, cool));

public static readonly INocturneSerializer<Person> DATABASE_SERIALIZER = NocturneSerializer.FromCodec(CODEC);
```

---

### Collections

You can then do all the fun stuff like `Insert`, `Find`, `FindOrNull`, `FindOrAdd`, `Delete`, `Nuke`, etc. by calling `Person.DB_COLLECTION`:

```csharp
var newPerson = new Person("John Person", 21, false);
Person.DB_COLLECTION.Insert("john", newPerson);

var john = Person.DB_COLLECTION.Find("john");
Person.DB_COLLECTION.Delete("john");

//fuck it, who needs prod anyway
Person.DB_COLLECTION.Nuke();
```

---


### Schema Migrations

If you change your model structure, Nocturne handles step-by-step migrations using byte buffers. For example, migrating from Version 0 (which had a FunFact string) to Version 1 (current):
```csharp

IMigrationStrategy.Migrations()
    .Add(0, buffer =>
    {
        // 1. Read the old schema format out of the buffer
        var name = BinaryCodecs.STRING.Read(buffer);
        var age = BinaryCodecs.INT.Read(buffer);
        var isCool = BinaryCodecs.BOOLEAN.Read(buffer);
        var _ = BinaryCodecs.STRING.Read(buffer); // Discard old "FunFact" string

        // 2. Write the clean data into the new schema format
        var newBuffer = Unpooled.Buffer();
        BinaryCodecs.STRING.Write(newBuffer, name);
        BinaryCodecs.INT.Write(newBuffer, age);
        BinaryCodecs.BOOLEAN.Write(newBuffer, isCool);

        return newBuffer; // buffer is automatically released if you choose to use pooled one
    })
    .Build()

```

You may also use `IMigrationStrategy.DeleteIfMigrationRequired()` which deletes the collection and creates new one with new schema if a migration is required

---

### Compaction

This is a log-style database, meaning it's append-only. Records do not get truly deleted from the database file, only marked as dead. 
Calling `Database.Compact()` will remove all records marked as dead or outdated and compact the file down. Remember to call it once in a while, otherwise your database will grow exponentially!

There are properties `CompactOnLaunch` which compacts the database file when the database launches and `AutomaticallyCompact` which compacts the database automatically when over 50% of its content is dead/outdated. Do keep in mind compacting does block the main thread and may take a good 500ms (or more depending on your data size)

If you are working with performance-critical stuff, I recommend managing compactions manually instead of relying on `AutomaticallyCompact`

---

### Backups

You may take a backup of the database by selecting the file and pressing CTRL + C followed by CTRL + V and adding "dkjglkg.bak" at the end of the file name 

**(pro tip: if the file name is already taken, smash your keyboard again. Repeat until you land on not taken file name)**

_**(ultra pro tip: if you are a real pro, you may even set up a CRON job for this!)**_

---

Lastly, this is a learning project I made practically in one sitting. You should probably not use it in production code...

