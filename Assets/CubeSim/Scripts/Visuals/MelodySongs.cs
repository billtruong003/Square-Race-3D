using UnityEngine;

namespace CubeSim.Visuals
{
    /// <summary>
    /// The songbook for the collision instrument: twenty public-domain pieces transcribed by hand
    /// into semitone offsets from each song's root. No recordings, no copyright exposure.
    ///
    /// Three rules shape the book. ACCURACY: hooks are transcribed from the actual scores - a
    /// melody nobody recognises is just noise - and several were cross-checked against published
    /// letter notation. REGISTER: each root sits where the original piece actually lives, so the
    /// tune lands in the register the ear knows it by. PENTATONIC-SOFT: diatonic major tunes get their out-of-scale
    /// notes nudged to the nearest major-pentatonic tone (contour preserved, hook intact) so dense
    /// contact storms stay consonant; tunes whose identity lives in chromatic notes - Fur Elise's
    /// trill, the Habanera's slide, ragtime pickups - are left verbatim, because snapping those
    /// changes the song's nature. 99 marks a phrase breath (a rest).
    /// </summary>
    public static class MelodySongs
    {
        public const int Rest = 99;

        public sealed class Song
        {
            public string Name;

            /// <summary>Root note frequency in Hz - each song sits where its melody reads best.</summary>
            public float RootFrequency;

            /// <summary>
            /// True applies the soft pentatonic pass: out-of-scale notes move to the nearest
            /// major-pentatonic tone, everything else stays. False plays the score verbatim.
            /// </summary>
            public bool PentatonicSoft;

            public int[] Notes;
        }

        /// <summary>Major pentatonic pitch classes - no semitone clashes, the music-ball scale.</summary>
        private static readonly int[] MajorPentatonic = { 0, 2, 4, 7, 9 };

        static MelodySongs()
        {
            foreach (Song song in Book)
            {
                if (!song.PentatonicSoft) continue;

                int previous = 0;
                for (int i = 0; i < song.Notes.Length; i++)
                {
                    if (song.Notes[i] == Rest) continue;
                    int original = song.Notes[i];
                    song.Notes[i] = SoftSnap(original, previous);
                    previous = original;
                }
            }
        }

        /// <summary>
        /// Nearest major-pentatonic tone; a tie resolves in the melody's direction of travel so
        /// the contour survives (an ascending F goes up to G, a descending one falls to E).
        /// </summary>
        private static int SoftSnap(int note, int previous)
        {
            int pitchClass = ((note % 12) + 12) % 12;

            int bestBelow = int.MinValue, bestAbove = int.MaxValue;
            foreach (int tone in MajorPentatonic)
            {
                for (int octave = -12; octave <= 12; octave += 12)
                {
                    int delta = tone + octave - pitchClass;
                    int candidate = note + delta;
                    if (delta == 0) return note;
                    if (delta < 0 && candidate > bestBelow) bestBelow = candidate;
                    if (delta > 0 && candidate < bestAbove) bestAbove = candidate;
                }
            }

            int downDistance = note - bestBelow;
            int upDistance = bestAbove - note;

            if (downDistance < upDistance) return bestBelow;
            if (upDistance < downDistance) return bestAbove;
            return note >= previous ? bestAbove : bestBelow;
        }

        public static readonly Song[] Book =
        {
            new Song
            {
                // Beethoven, 1810. The E/D#/E trill and both arpeggio answers, exact - including
                // the A that sits a sixth below the root, not a seventh.
                Name = "Fur Elise",
                RootFrequency = 659.26f, // E5
                PentatonicSoft = false,
                Notes = new[]
                {
                    0, -1, 0, -1, 0, -5, -2, -4, -7, 99,
                    -16, -12, -7, -5, 99,
                    -12, -8, -5, -4, 99,
                    -12, 0, -1, 0, -1, 0, -5, -2, -4, -7, 99,
                    -16, -12, -7, -5, 99,
                    -12, -4, -5, -7, 99,
                    // The B-section answer.
                    -4, -2, 0, 99,
                    1, 0, -2, -4, 99,
                    -2, -4, -5, -7, 99,
                    // Da capo.
                    0, -1, 0, -1, 0, -5, -2, -4, -7, 99,
                    -16, -12, -7, -5, 99,
                    -12, -8, -5, -4, 99,
                    -12, 0, -1, 0, -1, 0, -5, -2, -4, -7, 99,
                    -16, -12, -7, -5, 99,
                    -12, -4, -5, -7, 99,
                },
            },
            new Song
            {
                // Mozart, 1783. Alla turca: B A G# A C / D C B C E / F E D# E, then the leap to
                // B5 and the C6 crown - the exact rondo theme, twice, with the closing fall.
                Name = "Turkish March",
                RootFrequency = 493.88f, // B4
                PentatonicSoft = false,
                Notes = new[]
                {
                    0, -2, -3, -2, 1, 99,
                    3, 1, 0, 1, 5, 99,
                    6, 5, 4, 5, 12, 10, 9, 10, 99,
                    12, 10, 9, 10, 13, 99,
                    0, -2, -3, -2, 1, 99,
                    3, 1, 0, 1, 5, 99,
                    6, 5, 4, 5, 12, 10, 9, 10, 99,
                    12, 10, 9, 10, 13, 99,
                    // The closing descent.
                    13, 12, 10, 9, 10, 99,
                    6, 5, 4, 5, 99,
                    3, 1, 0, 1, 99,
                    1, 0, -2, -3, 0, 99,
                },
            },
            new Song
            {
                // Grieg, 1875. Theme twice low, lifted an octave, stretto, and the tumble home -
                // chromatic passing tones (the Bb, the F) kept, they ARE the creep.
                Name = "Mountain King",
                RootFrequency = 329.63f, // E4
                PentatonicSoft = false,
                Notes = new[]
                {
                    0, 2, 3, 5, 7, 3, 7, 99,
                    6, 2, 6, 99,
                    5, 1, 5, 99,
                    0, 2, 3, 5, 7, 3, 7, 99,
                    12, 7, 3, 7, 12, 99,
                    0, 2, 3, 5, 7, 3, 7, 99,
                    6, 2, 6, 99,
                    5, 1, 5, 99,
                    0, 2, 3, 5, 7, 3, 7, 12, 99,
                    12, 14, 15, 17, 19, 15, 19, 99,
                    18, 14, 18, 99,
                    17, 13, 17, 99,
                    12, 14, 15, 17, 19, 15, 19, 99,
                    24, 19, 15, 19, 24, 99,
                    12, 14, 15, 17, 19, 15, 19, 24, 99,
                    24, 22, 19, 17, 15, 14, 12, 99,
                    12, 11, 9, 8, 7, 5, 3, 2, 0, 99,
                    0, 3, 7, 12, 7, 3, 0, 99,
                },
            },
            new Song
            {
                // Brahms, 1869. G minor: D | G Bb G F# G-A-G, twice, then the C-D-Eb answer -
                // checked against published letter notation (d g A# g F# g a g / c d D# A# c...).
                Name = "Hungarian Dance",
                RootFrequency = 392.00f, // G4
                PentatonicSoft = false,
                Notes = new[]
                {
                    -5, 0, 3, 0, -1, 0, 2, 0, 99,
                    -5, 0, 3, 0, -1, 0, 2, 0, 99,
                    5, 7, 8, 3, 5, 3, 3, 2, 2, 99,
                    -5, 0, 3, 0, -1, 0, 2, 0, 99,
                    // Around again with the cadence fall.
                    -5, 0, 3, 0, -1, 0, 2, 0, 99,
                    5, 7, 8, 3, 5, 3, 3, 2, 2, 99,
                    3, 2, 0, -1, 0, 2, 0, 99,
                    -2, -4, -5, 0, 99,
                },
            },
            new Song
            {
                // Rossini, 1829. The call, the gallop twice, the high answer, the ride home.
                Name = "William Tell",
                RootFrequency = 261.63f, // C4
                PentatonicSoft = true,
                Notes = new[]
                {
                    0, 0, 0, 0, 4, 7, 99,
                    0, 0, 0, 0, 4, 7, 99,
                    12, 12, 12, 12, 9, 5, 9, 7, 4, 0, 99,
                    4, 7, 4, 7, 9, 7, 4, 7, 99,
                    12, 12, 12, 12, 9, 5, 9, 7, 4, 0, 99,
                    4, 7, 9, 7, 4, 0, 4, 2, 0, 99,
                    12, 14, 16, 16, 16, 14, 12, 14, 16, 14, 12, 9, 99,
                    9, 11, 12, 12, 12, 11, 9, 11, 12, 11, 9, 7, 99,
                    12, 12, 12, 12, 9, 5, 9, 7, 4, 0, 99,
                    0, 4, 7, 12, 16, 12, 7, 4, 0, 99,
                },
            },
            new Song
            {
                // Offenbach, 1858. The Galop infernal - full strain plus the high answer.
                Name = "Can Can",
                RootFrequency = 293.66f, // D4
                PentatonicSoft = true,
                Notes = new[]
                {
                    0, 0, 0, 0, 0, 2, 4, 4, 2, 0, 99,
                    4, 5, 7, 7, 7, 9, 7, 5, 4, 4, 4, 5, 4, 2, 99,
                    2, 4, 5, 5, 5, 7, 5, 4, 2, 2, 2, 4, 2, 0, 99,
                    4, 5, 7, 7, 7, 9, 7, 5, 4, 4, 4, 5, 4, 2, 99,
                    2, 4, 5, 4, 2, 0, 2, 4, 0, 99,
                    12, 12, 11, 12, 14, 12, 11, 12, 99,
                    16, 16, 14, 16, 17, 16, 14, 16, 99,
                    12, 12, 11, 12, 14, 12, 11, 12, 99,
                    17, 16, 14, 12, 11, 9, 7, 5, 4, 2, 0, 99,
                    0, 4, 7, 12, 16, 12, 7, 4, 0, 99,
                },
            },
            new Song
            {
                // Mozart, 1787. The fanfare, the C-A answer, the rising run, and the turn.
                Name = "Nachtmusik",
                RootFrequency = 392.00f, // G4
                PentatonicSoft = true,
                Notes = new[]
                {
                    0, -5, 0, -5, 0, -5, 0, 4, 7, 99,
                    5, 2, 5, 2, 5, 2, -1, 2, 7, 99,
                    0, 0, 4, 4, 7, 7, 12, 99,
                    12, 14, 12, 11, 12, 14, 12, 11, 12, 99,
                    7, 9, 11, 12, 11, 9, 7, 5, 4, 99,
                    4, 5, 7, 9, 7, 5, 4, 2, 0, 99,
                    0, -5, 0, -5, 0, -5, 0, 4, 7, 99,
                    0, 4, 7, 12, 16, 12, 7, 4, 0, 99,
                },
            },
            new Song
            {
                // Beethoven, 1824. The full AABA hymn plus the octave coda.
                Name = "Ode to Joy",
                RootFrequency = 261.63f, // C4
                PentatonicSoft = true,
                Notes = new[]
                {
                    4, 4, 5, 7, 7, 5, 4, 2, 0, 0, 2, 4, 4, 2, 2, 99,
                    4, 4, 5, 7, 7, 5, 4, 2, 0, 0, 2, 4, 2, 0, 0, 99,
                    2, 2, 4, 0, 2, 4, 5, 4, 0, 2, 4, 5, 4, 2, 0, 2, -5, 99,
                    4, 4, 5, 7, 7, 5, 4, 2, 0, 0, 2, 4, 2, 0, 0, 99,
                    16, 16, 17, 19, 19, 17, 16, 14, 12, 12, 14, 16, 14, 12, 12, 99,
                },
            },
            new Song
            {
                // Vivaldi, 1725. Spring's opening ritornello: E G# G# G# F# E B, and the answer.
                Name = "Spring",
                RootFrequency = 659.26f, // E5
                PentatonicSoft = true,
                Notes = new[]
                {
                    0, 4, 4, 4, 2, 0, 7, 99,
                    7, 5, 4, 5, 7, 5, 4, 2, 99,
                    0, 4, 4, 4, 2, 0, 7, 99,
                    7, 5, 4, 5, 7, 5, 4, 2, 0, 99,
                    // The trilling answer.
                    7, 9, 7, 9, 7, 5, 4, 2, 0, 99,
                    4, 5, 7, 9, 7, 5, 4, 2, 0, 99,
                    0, 4, 4, 4, 2, 0, 7, 99,
                    7, 5, 4, 2, 0, 99,
                },
            },
            new Song
            {
                // Tchaikovsky, 1892. The celesta's zigzag of falling thirds and the chromatic
                // tumble - kept verbatim, the chromatics are the whole spook.
                Name = "Sugar Plum",
                RootFrequency = 659.26f, // E5
                PentatonicSoft = false,
                Notes = new[]
                {
                    15, 12, 14, 11, 12, 8, 99,
                    15, 12, 14, 11, 12, 8, 99,
                    12, 11, 10, 9, 8, 7, 6, 5, 99,
                    7, 8, 10, 12, 15, 12, 99,
                    15, 12, 14, 11, 12, 8, 99,
                    12, 11, 10, 9, 8, 7, 99,
                    7, 4, 7, 3, 0, 99,
                },
            },
            new Song
            {
                // Tchaikovsky, 1876. The swan call: the long high note, the rising run under it,
                // and the cry an octave up.
                Name = "Swan Lake",
                RootFrequency = 220.00f, // A3
                PentatonicSoft = false,
                Notes = new[]
                {
                    12, 99, 5, 7, 8, 10, 12, 99,
                    12, 99, 5, 7, 8, 10, 12, 99,
                    15, 12, 10, 8, 7, 99,
                    12, 99, 5, 7, 8, 10, 12, 99,
                    // The cry.
                    24, 99, 17, 19, 20, 22, 24, 99,
                    24, 22, 20, 19, 17, 15, 14, 12, 99,
                    12, 8, 5, 3, 0, 99,
                },
            },
            new Song
            {
                // Bizet, 1875. The Habanera: the chromatic slide down from D, exact, then the
                // major answer. Snapping this one would erase the song.
                Name = "Habanera",
                RootFrequency = 293.66f, // D4
                PentatonicSoft = false,
                Notes = new[]
                {
                    12, 11, 10, 9, 99,
                    9, 7, 6, 7, 99,
                    5, 5, 5, 3, 2, 99,
                    2, 3, 5, 7, 5, 3, 2, 0, 99,
                    // Again.
                    12, 11, 10, 9, 99,
                    9, 7, 6, 7, 99,
                    5, 5, 5, 3, 2, 99,
                    2, 3, 5, 7, 5, 3, 2, 0, 99,
                    // The major refrain lift.
                    12, 14, 15, 17, 15, 14, 12, 99,
                    15, 14, 12, 10, 9, 7, 5, 3, 2, 0, 99,
                },
            },
            new Song
            {
                // Strauss II, 1866. The waltz call twice with its two answers, then the strain.
                Name = "Blue Danube",
                RootFrequency = 293.66f, // D4
                PentatonicSoft = true,
                Notes = new[]
                {
                    0, 4, 7, 7, 99, 19, 19, 99, 16, 16, 99,
                    0, 4, 7, 7, 99, 19, 19, 99, 17, 17, 99,
                    0, 4, 7, 7, 99, 19, 19, 99, 16, 16, 99,
                    0, 4, 7, 7, 99, 16, 16, 99, 14, 12, 99,
                    // The answering strain.
                    16, 14, 12, 11, 12, 14, 12, 99,
                    14, 12, 11, 9, 11, 12, 11, 99,
                    12, 11, 9, 7, 9, 11, 9, 99,
                    7, 4, 0, 4, 7, 12, 99,
                },
            },
            new Song
            {
                // Traditional English, 16th century. Verse and chorus whole; the G# cadences and
                // the dorian F# stay - they are the tune's colour.
                Name = "Greensleeves",
                RootFrequency = 440.00f, // A4
                PentatonicSoft = false,
                Notes = new[]
                {
                    0, 3, 5, 7, 8, 7, 5, 2, -2, 0, 2, 3, 0, 0, -1, 0, 2, -1, -5, 99,
                    0, 3, 5, 7, 8, 7, 5, 2, -2, 0, 2, 3, 2, 0, -1, -3, -1, 0, 99,
                    // Chorus.
                    10, 99, 10, 8, 7, 5, 2, -2, 0, 2, 3, 0, 0, -1, 0, -1, -3, 99,
                    10, 99, 10, 8, 7, 5, 2, -2, 0, 2, 3, 2, 0, -1, -3, -1, 0, 99,
                },
            },
            new Song
            {
                // Traditional Russian (Korobeiniki). Full AABB - the Tetris theme whole.
                Name = "Korobeiniki",
                RootFrequency = 440.00f, // A4
                PentatonicSoft = false,
                Notes = new[]
                {
                    7, 2, 3, 5, 3, 2, 0, 0, 3, 7, 5, 3, 99,
                    2, 3, 5, 7, 3, 0, 0, 99,
                    5, 8, 12, 10, 8, 7, 3, 7, 5, 3, 99,
                    2, 2, 3, 5, 7, 3, 0, 0, 99,
                    7, 2, 3, 5, 3, 2, 0, 0, 3, 7, 5, 3, 99,
                    2, 3, 5, 7, 3, 0, 0, 99,
                    5, 8, 12, 10, 8, 7, 3, 7, 5, 3, 99,
                    2, 2, 3, 5, 7, 3, 0, 0, 99,
                    // B: the low chant.
                    -5, -5, 0, 0, -2, -2, -4, -4, 99,
                    -5, -5, 0, 0, 3, 3, 7, 7, 99,
                    -5, -5, 0, 0, -2, -2, -4, -4, 99,
                    -4, -2, 0, -2, -4, -5, -5, 99,
                    -5, -5, 0, 0, -2, -2, -4, -4, 99,
                    -5, -5, 0, 0, 3, 3, 7, 7, 99,
                    7, 5, 3, 2, 0, 2, 3, 5, 7, 99,
                },
            },
            new Song
            {
                // Larionov, 1860. Kalinka: F# pickup then E-C#-D, E-C#-D, E-D-C#, B - checked
                // against published letter notation - plus the lyrical low verse.
                Name = "Kalinka",
                RootFrequency = 493.88f, // B4
                PentatonicSoft = false,
                Notes = new[]
                {
                    7, 5, 2, 3, 5, 2, 3, 5, 3, 2, 0, 99,
                    7, 5, 2, 3, 5, 2, 3, 5, 3, 2, 0, 99,
                    // The chorus drives faster each round - same figures, pressing on.
                    7, 5, 2, 3, 5, 2, 3, 5, 3, 2, 0, 99,
                    5, 2, 3, 5, 3, 2, 0, 7, 99,
                    // The lyrical verse, high and slow.
                    12, 10, 8, 7, 8, 10, 8, 7, 5, 99,
                    12, 10, 8, 7, 8, 10, 12, 99,
                    7, 5, 2, 3, 5, 2, 3, 5, 3, 2, 0, 99,
                },
            },
            new Song
            {
                // Pierpont, 1857. The full chorus, both endings.
                Name = "Jingle Bells",
                RootFrequency = 261.63f, // C4
                PentatonicSoft = true,
                Notes = new[]
                {
                    4, 4, 4, 99, 4, 4, 4, 99, 4, 7, 0, 2, 4, 99,
                    5, 5, 5, 5, 5, 4, 4, 4, 4, 2, 2, 4, 2, 99, 7, 99,
                    4, 4, 4, 99, 4, 4, 4, 99, 4, 7, 0, 2, 4, 99,
                    5, 5, 5, 5, 5, 4, 4, 4, 7, 7, 5, 2, 0, 99,
                },
            },
            new Song
            {
                // Traditional Welsh. Deck the Halls with the fa-la-la answers.
                Name = "Deck the Halls",
                RootFrequency = 261.63f, // C4
                PentatonicSoft = true,
                Notes = new[]
                {
                    7, 5, 4, 2, 0, 2, 4, 0, 99,
                    2, 4, 5, 2, 4, 2, 0, -1, 0, 99,
                    7, 5, 4, 2, 0, 2, 4, 0, 99,
                    2, 4, 5, 2, 4, 2, 0, -1, 0, 99,
                    // The lifted third line.
                    2, 4, 5, 4, 7, 5, 4, 2, 99,
                    4, 5, 7, 9, 11, 12, 7, 99,
                    7, 5, 4, 2, 0, 2, 4, 0, 99,
                    2, 4, 5, 2, 4, 2, 0, -1, 0, 99,
                },
            },
            new Song
            {
                // Joplin, 1902. The Entertainer's A strain, exact - the chromatic D-D#-E pickup
                // into the octave C hook is the whole identity, so no snapping.
                Name = "The Entertainer",
                RootFrequency = 523.25f, // C5
                PentatonicSoft = false,
                Notes = new[]
                {
                    -10, -9, -8, 0, -8, 0, -8, 0, 99,
                    0, 2, 3, 4, 0, 2, 4, 99,
                    -1, 2, 0, 99,
                    -10, -9, -8, 0, -8, 0, -8, 0, 99,
                    0, 2, 3, 4, 0, 2, 4, 99,
                    -1, 2, 0, 99,
                    // The answering phrase up top.
                    4, 5, 4, 2, 0, 2, 4, 0, 2, 0, 99,
                    4, 5, 4, 2, 0, 2, 4, 7, 4, 0, 99,
                    -10, -9, -8, 0, -8, 0, -8, 0, 99,
                    0, 2, 3, 4, 0, 2, 4, 99,
                    -1, 2, 0, 99,
                },
            },
            new Song
            {
                // Joplin, 1899. Maple Leaf Rag's A strain: the syncopated Eb-C rocking figure,
                // the chromatic climb, and the cascading fall out of the top.
                Name = "Maple Leaf Rag",
                RootFrequency = 415.30f, // Ab4
                PentatonicSoft = false,
                Notes = new[]
                {
                    7, 4, 7, 4, 7, 4, 0, 4, 99,
                    7, 4, 7, 4, 7, 4, 0, 4, 99,
                    0, 1, 2, 3, 4, 99,
                    19, 16, 12, 16, 12, 7, 12, 7, 4, 7, 4, 0, 99,
                    7, 4, 7, 4, 7, 4, 0, 4, 99,
                    0, 1, 2, 3, 4, 99,
                    12, 11, 12, 16, 12, 16, 19, 99,
                    16, 12, 7, 4, 5, 4, 1, 0, 99,
                },
            },
        };

        /// <summary>Deterministic pick when a seed is supplied; otherwise the caller rolls freely.</summary>
        public static Song Pick(int index) => Book[Mathf.Abs(index) % Book.Length];
    }
}
