using Codon.Binary;
using Nocturne.Database;
using Nocturne.Database.API;

namespace Nocturne.DataGenerators;

[NocturneCollection(key: "people", schemaVersion: 1, keySerializer: KeySerializers.STRING)]
public record Person(
    string Name,
    [NocturneCodec(CustomSerializers.VAR_INT)] // specific serializer, otherwise, auto choose from defaults
    [NocturneCodec(BinaryCodecs.VAR_INT)] // OR just use actual codon codec
    int Age,
    bool IsCool,
    List<Pet> Pets
)
{
}

[NocturneEmbeddedObject] // generates and registers codec/serializer but doesnt make collection or anything
public record Pet(Pet.PetType Type, string Name)
{
    public enum PetType
    {
        GuineaPig,
        Dog,
        Cat,
        AngryVenomousSawScaledSpider
    }
}

// later

public static class Program
{
    public static void Main(string[] args)
    {
        var database = new NocturneDatabase
        {
            FilePath = "./data/database.nocturne"
        };

        database.Open();

        // Auto generated property (extension maybe? have NocturneAutoGenerators.GetCollectionFor<Type>() and then the .NOCTURNE_COLLECTION can be just be extension field? (or method))
        Person.NOCTURNE_COLLECTION.Insert(new Person("syn", 21, true, [new Pet(Pet.PetType.GuineaPig, "Phanes")]));
    }
}
