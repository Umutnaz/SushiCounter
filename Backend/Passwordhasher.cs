using System.Text;

namespace Backend
{
    public static class PasswordHasherLinearProbing
    {
        private const int BucketLength = 100; // Antal mulige slots (0..99) som giver to-cifrede koder til tegn

        public static string Hash(string input) // Laver password i klartekst om til en numerisk hash-streng
        {
            if (input == null) // Tjek for null input så vi ikke crasher senere
                throw new ArgumentNullException(nameof(input)); // Smid fejl hvis input er null

            if (input.Length == 0) // Tom streng håndteres særskilt
                return string.Empty; // Tomt password giver tom hash-streng (kan ændres hvis du vil forbyde tomme passwords)

            var used = new bool[BucketLength]; // Husker hvilke slots (0..99) der allerede er brugt til tegn i dette password

            var sb = new StringBuilder(input.Length * 2); // StringBuilder til at bygge hash'en (2 cifre pr. tegn)

            foreach (char c in input) // Gennemløb alle tegn i passwordet
            {
                int h = c; // Start med at lave char om til int (Unicode-kode for tegnet)

                if (h < 0) // Sikrer positiv værdi (defensiv, char bør i praksis være ≥ 0)
                    h = -h; // Vend fortegn hvis det mod forventning er negativt

                h = h % BucketLength; // Startslot til lineær probing (giver værdi 0..99)
                int start = h; // Husk startposition så vi kan opdage fuldt loop

                while (used[h]) // Så længe slot'et allerede er brugt, fortsæt med lineær probing
                {
                    h = (h + 1) % BucketLength; // Gå én plads frem og wrap rundt med modulo

                    if (h == start) // Hvis vi er tilbage ved start er alle slots brugt
                        throw new InvalidOperationException("Ingen ledige hash-slots tilbage – password er for langt i forhold til BucketLength."); // Fejl hvis ingen ledige pladser
                }

                used[h] = true; // Marker det fundne slot som brugt til dette tegn

                string codeForChar = h.ToString("D2"); // Lav slot-nummeret om til to cifre (00..99)

                sb.Append(codeForChar); // Læg to-cifret kode for tegnet til den samlede hash-streng
            }

            return sb.ToString(); // Returnér den færdige hash-streng bestående kun af tal
        }
    }
}
