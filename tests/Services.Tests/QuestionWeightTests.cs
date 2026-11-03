using DynamicCube.Modules.Services.Definitions;
using DynamicCube.Modules.Services.Domain;
using Shouldly;

namespace DynamicCube.Modules.Services.Tests;

/// <summary>An answer's weight (points, grade) stays in the question bank: the form the applicant fills never carries it.</summary>
public sealed class QuestionWeightTests
{
    [Fact]
    public void The_form_shows_the_choices_without_their_weights()
    {
        var question = new Question
        {
            Code = "BOARD",
            NameAr = "مجلس الإدارة",
            NameEn = "Board",
            Type = QuestionType.Select,
            Options =
            [
                new QuestionOption { Value = "YES", LabelAr = "نعم", LabelEn = "Yes", Points = 10, Grade = 1 },
                new QuestionOption { Value = "NO", LabelAr = "لا", LabelEn = "No", Points = 0, Grade = 5 },
            ],
        };

        var snapshot = QuestionSnapshot.Of(question);

        snapshot.Options.Select(o => o.Value).ShouldBe(["YES", "NO"]);
        snapshot.Options.ShouldAllBe(o => o.Points == null && o.Grade == null);
        question.Options[0].Points.ShouldBe(10, "the bank keeps them");
    }
}
