using TheCoach.Application.Coaching.Domain;

namespace TheCoach.Application.Coaching.Persistence;

public static class ExerciseSeed
{
    public static IReadOnlyList<Exercise> GlobalExercises { get; } = BuildSeed();

    private static List<Exercise> BuildSeed()
    {
        var data = new[]
        {
            // Chest
            ("Barbell Bench Press", "Chest"), ("Dumbbell Bench Press", "Chest"),
            ("Incline Barbell Press", "Chest"), ("Incline Dumbbell Press", "Chest"),
            ("Decline Bench Press", "Chest"), ("Cable Fly", "Chest"),
            ("Dumbbell Fly", "Chest"), ("Push-Up", "Chest"),
            ("Dip", "Chest"), ("Pec Deck Machine", "Chest"),

            // Back
            ("Barbell Row", "Back"), ("Dumbbell Row", "Back"),
            ("Pull-Up", "Back"), ("Lat Pulldown", "Back"),
            ("Seated Cable Row", "Back"), ("T-Bar Row", "Back"),
            ("Deadlift", "Back"), ("Straight-Arm Pulldown", "Back"),
            ("Face Pull", "Back"), ("Chest-Supported Row", "Back"),

            // Shoulders
            ("Barbell Overhead Press", "Shoulders"), ("Dumbbell Shoulder Press", "Shoulders"),
            ("Lateral Raise", "Shoulders"), ("Front Raise", "Shoulders"),
            ("Rear Delt Fly", "Shoulders"), ("Cable Lateral Raise", "Shoulders"),
            ("Arnold Press", "Shoulders"), ("Upright Row", "Shoulders"),
            ("Machine Shoulder Press", "Shoulders"), ("Barbell Shrug", "Shoulders"),

            // Biceps
            ("Barbell Curl", "Biceps"), ("Dumbbell Curl", "Biceps"),
            ("Hammer Curl", "Biceps"), ("Preacher Curl", "Biceps"),
            ("Cable Curl", "Biceps"), ("Incline Dumbbell Curl", "Biceps"),
            ("Concentration Curl", "Biceps"), ("Machine Curl", "Biceps"),
            ("Reverse Curl", "Biceps"), ("EZ-Bar Curl", "Biceps"),

            // Triceps
            ("Tricep Pushdown", "Triceps"), ("Skull Crusher", "Triceps"),
            ("Close-Grip Bench Press", "Triceps"), ("Overhead Tricep Extension", "Triceps"),
            ("Cable Overhead Extension", "Triceps"), ("Diamond Push-Up", "Triceps"),
            ("Tricep Kickback", "Triceps"), ("Machine Dip", "Triceps"),
            ("Rope Pushdown", "Triceps"), ("French Press", "Triceps"),

            // Legs – Quads
            ("Barbell Squat", "Quadriceps"), ("Hack Squat", "Quadriceps"),
            ("Leg Press", "Quadriceps"), ("Leg Extension", "Quadriceps"),
            ("Bulgarian Split Squat", "Quadriceps"), ("Front Squat", "Quadriceps"),
            ("Goblet Squat", "Quadriceps"), ("Wall Sit", "Quadriceps"),
            ("Step-Up", "Quadriceps"), ("Lunge", "Quadriceps"),

            // Legs – Hamstrings / Glutes
            ("Romanian Deadlift", "Hamstrings"), ("Leg Curl", "Hamstrings"),
            ("Nordic Hamstring Curl", "Hamstrings"), ("Good Morning", "Hamstrings"),
            ("Hip Hinge", "Hamstrings"), ("Glute Bridge", "Glutes"),
            ("Barbell Hip Thrust", "Glutes"), ("Cable Kickback", "Glutes"),
            ("Sumo Deadlift", "Glutes"), ("Clamshell", "Glutes"),

            // Calves
            ("Standing Calf Raise", "Calves"), ("Seated Calf Raise", "Calves"),
            ("Donkey Calf Raise", "Calves"), ("Single-Leg Calf Raise", "Calves"),

            // Core
            ("Plank", "Core"), ("Ab Wheel Rollout", "Core"),
            ("Hanging Leg Raise", "Core"), ("Cable Crunch", "Core"),
            ("Russian Twist", "Core"), ("Dead Bug", "Core"),
            ("Pallof Press", "Core"), ("Suitcase Carry", "Core"),
            ("Hollow Body Hold", "Core"), ("Bird Dog", "Core"),

            // Olympic / Power
            ("Power Clean", "Full Body"), ("Hang Clean", "Full Body"),
            ("Snatch", "Full Body"), ("Clean and Jerk", "Full Body"),
            ("Box Jump", "Full Body"), ("Broad Jump", "Full Body"),
            ("Medicine Ball Slam", "Full Body"), ("Kettlebell Swing", "Full Body"),
            ("Barbell Complex", "Full Body"), ("Farmer Carry", "Full Body"),

            // Cardio / Conditioning
            ("Assault Bike Intervals", "Cardio"), ("Rowing Intervals", "Cardio"),
            ("Treadmill Sprint", "Cardio"), ("Ski Erg", "Cardio"),
            ("Jump Rope", "Cardio"), ("Sled Push", "Cardio"),
            ("Battle Ropes", "Cardio"), ("Stair Climber", "Cardio"),
        };

        return data.Select(d => new Exercise
        {
            Id = Guid.CreateVersion7(),
            Name = d.Item1,
            MuscleGroup = d.Item2,
            TenantId = Guid.Empty
        }).ToList();
    }
}
