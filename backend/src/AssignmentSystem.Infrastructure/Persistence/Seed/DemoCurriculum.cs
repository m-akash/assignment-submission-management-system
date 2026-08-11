using System.Globalization;

namespace AssignmentSystem.Infrastructure.Persistence.Seed;

/// <summary>A subject in the catalogue, and the grades that study it.</summary>
/// <param name="Name">Displayed as-is. Repeated across grades — the code is what separates them.</param>
/// <param name="CodePrefix">Joined with the grade to form the course code: "BAN" + 6 → <c>BAN601</c>.</param>
internal sealed record DemoSubject(string Name, string CodePrefix, int[] Levels);

/// <summary>
/// One piece of coursework the demo teacher has authored, before it becomes an
/// <see cref="Domain.Assignments.Assignment"/>. The brief the students read is built from
/// <paramref name="Focus"/> and <paramref name="Tasks"/>, and so is the attachment — which is
/// why the attached worksheet always carries exactly the questions the description promises.
/// </summary>
/// <param name="DueInDays">Deadline as an offset from seed time, so the demo never goes stale.</param>
/// <param name="FigureVariant">Which curve the generated PNG figure plots (see <c>DemoPng</c>).</param>
internal sealed record DemoBrief(
    string Title,
    decimal MaxMarks,
    int DueInDays,
    string Focus,
    string[] Tasks,
    int FigureVariant);

/// <summary>
/// The shape of the seeded school: which grades exist, which subjects each of them studies,
/// which of those offerings an admin has already mapped a teacher onto, and the coursework the
/// demo teacher has set for the offerings that are theirs.
///
/// Data only — <see cref="DbSeeder"/> turns it into rows. Kept apart from the seeder because it
/// is the part that gets edited: adding a grade, moving a teacher or rewriting a brief happens
/// here, and none of it needs the persistence code re-read.
/// </summary>
internal static class DemoCurriculum
{
    /// <summary>Grades 6–12, two sections each — the range a Bangladeshi secondary school covers.</summary>
    public static readonly int[] Levels = [6, 7, 8, 9, 10, 11, 12];

    public static readonly string[] Sections = ["A", "B"];

    public const int StudentsPerSection = 5;

    /// <summary>
    /// Where the documented <c>student@assignment.test</c> login sits. Chosen so the demo
    /// teacher's own offerings include this class — see <see cref="TeachingPlan"/> — because a
    /// student only ever sees work set for a class they are enrolled in.
    /// </summary>
    public const int DemoStudentLevel = 10;

    public const string DemoStudentSection = "A";

    /// <summary>The demo teacher's index into the seeder's teacher list.</summary>
    public const int DemoTeacherIndex = 0;

    // ── Subject catalogue ─────────────────────────────────────────────────────
    public const string Bangla = "Bangla";
    public const string English = "English";
    public const string GeneralMathematics = "General Mathematics";
    public const string GeneralScience = "General Science";
    public const string HigherMathematics = "Higher Mathematics";
    public const string Physics = "Physics";
    public const string Chemistry = "Chemistry";
    public const string Biology = "Biology";

    private static readonly int[] LowerLevels = [6, 7, 8];
    private static readonly int[] UpperLevels = [9, 10, 11, 12];

    /// <summary>
    /// Every subject is a course *per grade*, not one course shared by all of them: grade 6
    /// Bangla and grade 9 Bangla are different syllabuses taught to different rooms, and giving
    /// them one row would mean one teacher mapping and one assignment list for both.
    ///
    ///   • Bangla and English run the whole way through, 6–12.
    ///   • General Mathematics and General Science are the lower grades' subjects (6–8).
    ///   • Higher Mathematics, Physics, Chemistry and Biology begin at grade 9, where the
    ///     combined science splits into the three separate ones.
    /// </summary>
    public static readonly DemoSubject[] Subjects =
    [
        new(Bangla, "BAN", Levels),
        new(English, "ENG", Levels),
        new(GeneralMathematics, "GMATH", LowerLevels),
        new(GeneralScience, "GSCI", LowerLevels),
        new(HigherMathematics, "HMATH", UpperLevels),
        new(Physics, "PHY", UpperLevels),
        new(Chemistry, "CHE", UpperLevels),
        new(Biology, "BIO", UpperLevels),
    ];

    /// <summary>The subjects a given grade studies, in catalogue order.</summary>
    public static IEnumerable<DemoSubject> SubjectsFor(int level) =>
        Subjects.Where(s => s.Levels.Contains(level));

    /// <summary>
    /// Course code for a subject at a grade: prefix, grade, then a two-digit sequence — so
    /// <c>BAN601</c> and <c>ENG601</c> for grade 6, <c>BAN1101</c> for grade 11. The grade is
    /// readable straight off the code, which is the whole point of encoding it there.
    /// </summary>
    public static string CodeFor(DemoSubject subject, int level) =>
        $"{subject.CodePrefix}{level.ToString(CultureInfo.InvariantCulture)}01";

    // ── Teaching mappings ─────────────────────────────────────────────────────

    /// <summary>
    /// Two mapped offerings per section, and no more. Every class studies its full subject
    /// list, but only these 28 offerings arrive with a teacher on them — the remaining 44 are
    /// left blank on purpose, so the admin's teacher-mapping screen has genuine work waiting
    /// instead of a school that is already fully wired.
    ///
    /// Teachers are named by index into the seeder's list, and each one keeps to their own
    /// subjects: the demo teacher (index 0) is the mathematics and physics master, which is why
    /// their name appears against General Mathematics in the lower grades and Higher
    /// Mathematics and Physics in the upper ones. Both of grade 10 section A's mapped offerings
    /// are theirs, so the demo student is taught two of the demo teacher's courses.
    /// </summary>
    public static readonly (int Level, string Section, string Subject, int TeacherIndex)[] TeachingPlan =
    [
        (6, "A", Bangla, 1), (6, "A", GeneralMathematics, 0),
        (6, "B", English, 2), (6, "B", GeneralScience, 3),
        (7, "A", Bangla, 6), (7, "A", GeneralMathematics, 0),
        (7, "B", English, 6), (7, "B", GeneralScience, 3),
        (8, "A", GeneralMathematics, 0), (8, "A", GeneralScience, 3),
        (8, "B", Bangla, 1), (8, "B", English, 2),
        (9, "A", HigherMathematics, 4), (9, "A", Physics, 0),
        (9, "B", Chemistry, 5), (9, "B", Biology, 3),
        (10, "A", HigherMathematics, 0), (10, "A", Physics, 0),
        (10, "B", Chemistry, 5), (10, "B", Biology, 3),
        (11, "A", HigherMathematics, 4), (11, "A", Chemistry, 5),
        (11, "B", Physics, 0), (11, "B", Biology, 3),
        (12, "A", Bangla, 1), (12, "A", English, 2),
        (12, "B", HigherMathematics, 0), (12, "B", Physics, 4),
    ];

    // ── Coursework ────────────────────────────────────────────────────────────

    /// <summary>
    /// Three briefs for every offering the demo teacher holds — the first published, the other
    /// two still drafts, so that login shows both halves of the authoring workflow. Keyed by
    /// (subject, grade) rather than written once per subject: grade 6 fractions and grade 8
    /// mensuration are both General Mathematics, and a brief that ignored the grade would be
    /// wrong for one of them.
    /// </summary>
    public static IReadOnlyList<DemoBrief> BriefsFor(string subject, int level) =>
        Briefs.TryGetValue((subject, level), out var briefs)
            ? briefs
            : throw new InvalidOperationException(
                $"No seeded coursework for {subject} at grade {level}. Every offering in " +
                $"{nameof(TeachingPlan)} that belongs to the demo teacher needs three briefs here.");

    /// <summary>How the work is to be handed in — the same three rules on every brief.</summary>
    public static readonly string[] SubmissionRules =
    [
        "Attach one file. A scan or a clear photograph of your written work is fine, so long as every page is readable.",
        "Show your working. A bare answer earns no marks here, even when the answer is right.",
        "Put your name, class and roll number at the top of the first page.",
    ];

    /// <summary>
    /// What each student wrote in the text box alongside their attachment. Five of them, taken
    /// in turn across a class, so a teacher's marking queue does not read as one student
    /// copied five times.
    /// </summary>
    public static readonly string[] AnswerNotes =
    [
        "All four sections attempted. The full working is in the attached file — I had to redo Section B twice before the numbers agreed.",
        "Finished every section. Section C was the hardest, and I have written a note beside the one step I am still unsure about.",
        "My working is attached. Sections A to C are complete; in the last section I have shown the method but ran short of time on the final part.",
        "Completed all sections and checked each answer by substitution. The diagrams are on the second page of the attachment.",
        "Answers attached. I have written the formula I used above every calculation, as the brief asked.",
    ];

    /// <summary>
    /// Marks as a fraction of the assignment's maximum, with the feedback that goes with that
    /// band. Paired positionally with <see cref="AnswerNotes"/> so the comment matches what the
    /// student said they did.
    /// </summary>
    public static readonly (decimal Fraction, string Feedback)[] GradeBands =
    [
        (0.92m, "Excellent — every part correct and the working is easy to follow. Keep setting it out like this."),
        (0.85m, "Strong throughout. The only slip is a sign error in Section C; find it and correct it in your notebook."),
        (0.76m, "The method is sound, but the last section is thin. Finish it properly and show me before the next set."),
        (0.68m, "Right approach in most parts; the arithmetic let you down twice. Redo the two questions I have circled."),
        (0.58m, "You have the main idea, but several steps are missing and the units are absent in Section B. Come and see me this week."),
    ];

    private static readonly Dictionary<(string Subject, int Level), DemoBrief[]> Briefs = new()
    {
        // ── General Mathematics ───────────────────────────────────────────────
        [(GeneralMathematics, 6)] =
        [
            new("Fractions - Comparing, Adding and Subtracting", 20m, 6,
                "This set is on fractions with unlike denominators: putting them in order first, then adding and subtracting them.",
                [
                    "Arrange each of the five groups in Section A in ascending order, and write down the common denominator you used to compare them.",
                    "Work out the ten additions and subtractions in Section B, reducing every answer to its lowest terms.",
                    "Solve the four word problems in Section C. Write the fraction sentence first, then the calculation.",
                    "For problem C4, draw the number line you used and mark your answer on it.",
                ], 2),
            new("Decimals and Place Value", 20m, 9,
                "We move from fractions to decimals this week — reading place value correctly, then converting between the two forms.",
                [
                    "Write each of the eight numbers in Section A in words, and state the place value of the digit that is underlined.",
                    "Convert the ten fractions in Section B to decimals, and the ten decimals in Section C to fractions in lowest terms.",
                    "Round every number in Section D to the nearest tenth and to the nearest hundredth, and say which rounding changes the value more.",
                    "Complete the shopping table in Section E: add the amounts, then check your total by rounding each price first.",
                ], 0),
            new("Ratio and Proportion Word Problems", 25m, 12,
                "Ratio is where arithmetic starts describing real situations, so every answer here needs a sentence as well as a number.",
                [
                    "Simplify the twelve ratios in Section A, and write each one in the form 1 : n where that is possible.",
                    "Divide the six quantities in Section B in the ratios given, and check that your parts add back to the whole.",
                    "Solve the five direct-proportion problems in Section C by the unitary method — show the value of one unit each time.",
                    "Answer Section D in full sentences: state the ratio, the calculation, and what the answer means for the situation described.",
                ], 2),
        ],
        [(GeneralMathematics, 7)] =
        [
            new("Integers and the Number Line", 20m, 6,
                "Negative numbers behave exactly like positive ones until you multiply and divide them, which is where most of this set lives.",
                [
                    "Place all fourteen integers from Section A on a single number line drawn to scale.",
                    "Evaluate the twenty expressions in Section B, writing the sign rule you applied beside each answer.",
                    "Fill in the missing integers in the four magic squares of Section C so that every row, column and diagonal sums to the same total.",
                    "Answer the three temperature-and-altitude problems in Section D, stating clearly what zero represents in each one.",
                ], 2),
            new("Simplifying Algebraic Expressions", 25m, 9,
                "An expression is simplified only when no two like terms are left, and that is the one thing being marked here.",
                [
                    "Collect like terms in the twelve expressions of Section A.",
                    "Expand and then simplify the eight bracketed expressions in Section B, taking care with the sign in front of each bracket.",
                    "Substitute the given values into the six expressions of Section C, showing the substitution line before you evaluate.",
                    "Write an expression for each of the four situations described in Section D, and say what your letter stands for.",
                ], 0),
            new("Percentage, Profit and Loss", 30m, 12,
                "Every question here turns on the same idea: a percentage is always a percentage of something, and naming that something is half the work.",
                [
                    "Convert between the fractions, decimals and percentages in the table of Section A.",
                    "Find the profit or loss, and the profit or loss percentage, for the ten transactions in Section B.",
                    "Work out the selling price in Section C from the cost price and the required profit percentage.",
                    "Answer the four discount problems in Section D, stating the base amount you took the percentage of each time.",
                ], 2),
        ],
        [(GeneralMathematics, 8)] =
        [
            new("Linear Equations in One Variable", 25m, 6,
                "The rule for this set is one step per line: whatever you do to one side, do to the other, and write it down.",
                [
                    "Solve the fifteen equations in Section A, showing every step and checking each root by substitution.",
                    "Solve the six equations in Section B that carry brackets or fractions, clearing the denominators first.",
                    "Form and solve an equation for each of the five word problems in Section C, defining your variable before you start.",
                    "Explain in two or three sentences why the equation in C6 has no solution at all.",
                ], 2),
            new("Quadrilaterals and Their Properties", 20m, 10,
                "This set is about proof rather than measurement, so a drawing counts as evidence only when the reasoning beside it names the property used.",
                [
                    "Complete the property table in Section A for the six quadrilaterals listed.",
                    "Find the unknown angles in the eight figures of Section B, naming the property that justifies each value.",
                    "Construct the three quadrilaterals described in Section C with ruler and compass, leaving every construction arc visible.",
                    "Prove the statement in Section D: the diagonals of a rhombus bisect each other at right angles.",
                ], 1),
            new("Mensuration - Area, Surface Area and Volume", 30m, 13,
                "Units are marked here as heavily as the arithmetic: a volume answered in square centimetres scores nothing.",
                [
                    "Find the area of the eight composite figures in Section A by splitting each one into shapes you already know.",
                    "Calculate the total surface area and the volume of the six solids in Section B, writing the formula before you substitute.",
                    "Answer the four practical problems in Section C — the water tank, the cylindrical pipe, the cuboidal room and the conical heap.",
                    "State the units for every answer, and add a one-line check of whether each result is a sensible size.",
                ], 0),
        ],

        // ── Physics ───────────────────────────────────────────────────────────
        [(Physics, 9)] =
        [
            new("Motion - Distance, Displacement, Speed and Velocity", 25m, 6,
                "The whole of this set turns on one distinction: distance and speed have no direction, displacement and velocity do.",
                [
                    "Define each of the four quantities in the title, and give one example where distance and displacement differ.",
                    "Solve the ten numerical problems in Section B, writing the formula, the substitution and the unit on separate lines.",
                    "Draw the distance-time and velocity-time graphs for the journey in Section C, and find the acceleration from the slope of the second one.",
                    "Use the area under that velocity-time graph to find the total displacement, and compare it with the distance travelled.",
                ], 2),
            new("Newton's Three Laws - Problem Set", 30m, 9,
                "Every problem here starts with a free-body diagram; the marks for the numerical answer follow the marks for that diagram.",
                [
                    "State each of the three laws in your own words, and name an everyday situation that shows it.",
                    "Draw a free-body diagram for each of the six situations in Section B before you write a single equation.",
                    "Solve the eight problems in Section C on force, mass and acceleration, keeping every quantity in SI units.",
                    "Answer Section D on momentum and impulse, and explain why a longer collision time means a smaller force.",
                ], 0),
            new("Work, Power and Energy", 25m, 12,
                "Work is done only when a force moves its point of application, and several questions here are built to catch the cases where it is not.",
                [
                    "Decide for each of the eight situations in Section A whether work is done, and justify your answer in one sentence.",
                    "Calculate the work done and the power developed in the six problems of Section B.",
                    "Use conservation of energy to solve the four problems in Section C, naming the two forms of energy involved each time.",
                    "Answer Section D: explain where the lost energy goes in the pendulum experiment described.",
                ], 3),
        ],
        [(Physics, 10)] =
        [
            new("Refraction of Light and the Lens Formula", 30m, 7,
                "The ray diagram is the argument and the formula is only the check, so this set asks for both on every question.",
                [
                    "State the laws of refraction, and define refractive index in both its absolute and its relative form.",
                    "Draw accurate ray diagrams for the six object positions in Section B, marking the principal focus and the centre of curvature.",
                    "Use the lens and magnification formulae to solve the eight problems in Section C, with your sign convention stated at the top of the page.",
                    "Answer Section D on the human eye: explain myopia and hypermetropia, and the lens each one needs.",
                ], 1),
            new("Ohm's Law and Series-Parallel Circuits", 30m, 10,
                "Most mistakes in this topic are arithmetical rather than electrical, which is why every question asks for the equivalent resistance first.",
                [
                    "State Ohm's law, and the conditions under which it actually holds.",
                    "Find the equivalent resistance of the eight networks in Section B, redrawing each one in a simpler form as you go.",
                    "Calculate the current through and the potential difference across every resistor in the four circuits of Section C.",
                    "Answer Section D on electrical power and energy, including the cost of running the appliance described for one month.",
                ], 2),
            new("Sound - Wavelength, Frequency and Echo", 25m, 13,
                "One relation, speed = frequency x wavelength, does most of the work here; the rest is care over which medium the sound is travelling in.",
                [
                    "Define wavelength, frequency, time period and amplitude, and mark each of them on a labelled wave diagram.",
                    "Solve the ten problems in Section B with the wave relation, being explicit about the speed of sound in the medium given.",
                    "Answer the four echo and sonar problems in Section C, remembering that the sound covers the distance twice.",
                    "Explain in Section D why sound cannot travel through a vacuum, and describe an experiment that demonstrates it.",
                ], 1),
        ],
        [(Physics, 11)] =
        [
            new("Vectors and Projectile Motion", 35m, 7,
                "A projectile is two independent one-dimensional problems sharing a clock, and every solution here should be laid out that way.",
                [
                    "Resolve the eight vectors in Section A into components, then find the resultant of each of the four sets in Section B.",
                    "Derive the equation of the trajectory, the time of flight, the maximum height and the range for a projectile launched at an angle.",
                    "Solve the six numerical problems in Section C, treating the horizontal and vertical motions in separate columns.",
                    "Answer Section D: show that the range is the same for complementary angles of projection.",
                ], 1),
            new("Rotational Motion - Torque and Moment of Inertia", 35m, 11,
                "Every linear quantity you already know has a rotational partner, and the first task is to write that correspondence down.",
                [
                    "Complete the table in Section A, pairing each linear quantity with its rotational equivalent.",
                    "Calculate the moment of inertia of the six bodies in Section B about the axes shown, using the parallel-axis theorem where it is needed.",
                    "Solve the six torque and angular-acceleration problems in Section C.",
                    "Answer Section D on angular momentum: account for the skater's spin quantitatively, not only in words.",
                ], 0),
            new("First Law of Thermodynamics - Problem Set", 30m, 14,
                "Sign conventions decide most of the marks here, so state yours at the top of the first page and then keep to it.",
                [
                    "State the first law, and define internal energy, heat and work together with their sign conventions.",
                    "Complete the table in Section B, filling in the missing one of heat, work and change in internal energy for each of the ten processes.",
                    "Solve the four problems in Section C on isothermal and adiabatic processes, deriving the work done in each case.",
                    "Answer Section D: explain why the two specific heats of a gas differ, and derive the relation between them.",
                ], 3),
        ],

        // ── Higher Mathematics ────────────────────────────────────────────────
        [(HigherMathematics, 10)] =
        [
            new("Trigonometric Ratios and Identities", 30m, 6,
                "Identities are proved, not checked on a calculator, so each proof here has to move from one side to the other by algebra alone.",
                [
                    "Complete the table of ratios for the standard angles in Section A from memory, then check it against the attached sheet.",
                    "Prove the twelve identities in Section B, working on one side only and naming the identity you use at each step.",
                    "Solve the eight equations in Section C for angles between 0 and 360 degrees, giving every solution in that range.",
                    "Answer the four height-and-distance problems in Section D, each with a clearly labelled diagram.",
                ], 1),
            new("Coordinate Geometry - The Straight Line", 30m, 10,
                "A line has several forms and choosing the convenient one is most of the skill, so say why you chose the form you did.",
                [
                    "Find the distance, the midpoint and the gradient for the ten pairs of points in Section A.",
                    "Write the equation of each line described in Section B, giving your answer in the general form ax + by + c = 0.",
                    "Find the point of intersection, the angle between the lines, and the perpendicular distances asked for in Section C.",
                    "Answer Section D: prove that the three medians of the given triangle are concurrent, and find that point.",
                ], 2),
            new("Sets, Relations and Functions", 25m, 13,
                "This set moves from listing elements to reasoning about whole sets, and the notation is marked as closely as the answers.",
                [
                    "Write the twelve sets in Section A in both listing form and set-builder form.",
                    "Prove the six set identities in Section B, and illustrate two of them with Venn diagrams.",
                    "Decide for each relation in Section C whether it is a function, and give its domain and its range.",
                    "Sketch the four functions in Section D on graph paper, marking the intercepts and stating where each one is increasing.",
                ], 0),
        ],
        [(HigherMathematics, 12)] =
        [
            new("Techniques of Definite Integration", 40m, 8,
                "Each question is chosen to fit one technique — substitution, parts, or partial fractions — and naming the technique before you start is part of the answer.",
                [
                    "Evaluate the ten integrals in Section A by substitution, showing the change of limits explicitly.",
                    "Evaluate the eight integrals in Section B by parts, stating your choice of the two factors and why you made it.",
                    "Split the six rational functions in Section C into partial fractions, then integrate them.",
                    "Use the properties of definite integrals in Section D to evaluate four integrals without finding an antiderivative at all.",
                ], 0),
            new("First-Order Differential Equations", 40m, 11,
                "Classify before you solve: this set is separable equations, homogeneous equations and linear equations, in that order.",
                [
                    "Classify each of the fifteen equations in Section A, naming the method you would use on it.",
                    "Solve the eight separable equations in Section B, and give the particular solution wherever an initial condition is stated.",
                    "Solve the five linear equations in Section C with an integrating factor, showing how you found it.",
                    "Answer the three modelling problems in Section D on growth, decay and cooling, and interpret the constant in each answer.",
                ], 3),
            new("Complex Numbers and De Moivre's Theorem", 35m, 14,
                "Everything here is easier in polar form, and the questions are ordered so that this becomes obvious rather than being asserted.",
                [
                    "Write the twelve complex numbers in Section A in modulus-argument form, taking the principal argument each time.",
                    "Use De Moivre's theorem to evaluate the eight powers and roots in Section B.",
                    "Find all the roots asked for in Section C and plot them on an Argand diagram, commenting on their symmetry.",
                    "Prove the two identities in Section D, then answer the locus question at the end.",
                ], 1),
        ],
    };
}
