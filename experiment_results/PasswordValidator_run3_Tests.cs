using System;
using System.Collections.Generic;
using MutationAgentWorkflow.Sample;
using Xunit;

public class PasswordValidatorTests
{
    [Fact]
    public void IsValid_NullPassword_ReturnsFalse()
    {
        // Arrange
        var validator = new PasswordValidator();
        string password = null;

        // Act
        var result = validator.IsValid(password);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ab1!cde")]
    [InlineData("Ab1!cdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890")]
    [InlineData("password")]
    [InlineData("PASSWORD1!")]
    [InlineData("password1!")]
    [InlineData("PASSWORD!")]
    [InlineData("Password!")]
    [InlineData("Password1")]
    public void IsValid_InvalidPasswords_ReturnsFalse(string password)
    {
        // Arrange
        var validator = new PasswordValidator();

        // Act
        var result = validator.IsValid(password);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValid_ValidPassword_ReturnsTrue()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "Valid1!A";

        // Act
        var result = validator.IsValid(password);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("Ab1!cdef", 7)]
    [InlineData("Ab1!cdefghij", 8)]
    [InlineData("Ab1!cdefghijklmn", 9)]
    public void GetStrengthScore_LengthThresholds_AddsExpectedScore(string password, int expectedScore)
    {
        // Arrange
        var validator = new PasswordValidator();

        // Act
        var result = validator.GetStrengthScore(password);

        // Assert
        Assert.Equal(expectedScore, result);
    }

    [Fact]
    public void GetStrengthScore_NullPassword_ReturnsZero()
    {
        // Arrange
        var validator = new PasswordValidator();
        string password = null;

        // Act
        var result = validator.GetStrengthScore(password);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void GetStrengthScore_CommonPassword_ReturnsExpectedScore()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "password";

        // Act
        var result = validator.GetStrengthScore(password);

        // Assert
        Assert.Equal(2, result);
    }

    [Fact]
    public void GetStrengthScore_StrongPassword_ReturnsExpectedScore()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "Abcdef1!";

        // Act
        var result = validator.GetStrengthScore(password);

        // Assert
        Assert.Equal(7, result);
    }

    [Theory]
    [InlineData("ab1!", "Fair")]
    [InlineData("Ab1!cde", "Fair")]
    [InlineData("Ab1!cdef", "Strong")]
    [InlineData("Ab1!cdefghij", "Very Strong")]
    [InlineData("Ab1!cdefghijklmn", "Very Strong")]
    [InlineData("Abcdef1!Ghij", "Very Strong")]
    public void GetStrengthLabel_ScoreBoundaries_ReturnsExpectedLabel(string password, string expectedLabel)
    {
        // Arrange
        var validator = new PasswordValidator();

        // Act
        var result = validator.GetStrengthLabel(password);

        // Assert
        Assert.Equal(expectedLabel, result);
    }

    [Theory]
    [InlineData(null, "Password cannot be empty.")]
    [InlineData("", "Password cannot be empty.")]
    public void GetValidationErrors_NullOrEmptyPassword_ReturnsExpectedErrorMessage(string password, string expectedError)
    {
        // Arrange
        var validator = new PasswordValidator();

        // Act
        var result = validator.GetValidationErrors(password);

        // Assert
        Assert.Single(result);
        Assert.Equal(expectedError, result[0]);
    }

    [Fact]
    public void GetValidationErrors_TooShortPassword_ReturnsLengthError()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "Ab1!cde";

        // Act
        var result = validator.GetValidationErrors(password);

        // Assert
        Assert.Contains("Password must be at least 8 characters.", result);
    }

    [Fact]
    public void GetValidationErrors_TooLongPassword_ReturnsMaxLengthError()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "Ab1!cdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890XYZ";

        // Act
        var result = validator.GetValidationErrors(password);

        // Assert
        Assert.Contains("Password must be at most 64 characters.", result);
    }

    [Fact]
    public void GetValidationErrors_MissingUppercase_ReturnsUppercaseError()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "valid1!a";

        // Act
        var result = validator.GetValidationErrors(password);

        // Assert
        Assert.Contains("Password must contain at least one uppercase letter.", result);
    }

    [Fact]
    public void GetValidationErrors_MissingLowercase_ReturnsLowercaseError()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "VALID1!A";

        // Act
        var result = validator.GetValidationErrors(password);

        // Assert
        Assert.Contains("Password must contain at least one lowercase letter.", result);
    }

    [Fact]
    public void GetValidationErrors_MissingDigit_ReturnsDigitError()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "Valid!Ab";

        // Act
        var result = validator.GetValidationErrors(password);

        // Assert
        Assert.Contains("Password must contain at least one digit.", result);
    }

    [Fact]
    public void GetValidationErrors_MissingSpecialCharacter_ReturnsSpecialCharacterError()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "Valid1Ab";

        // Act
        var result = validator.GetValidationErrors(password);

        // Assert
        Assert.Contains("Password must contain at least one special character.", result);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("PASSWORD")]
    [InlineData("Password")]
    [InlineData("12345678")]
    [InlineData("QWERTY")]
    public void IsCommonPassword_CommonPasswordsDifferentCasing_ReturnsTrue(string password)
    {
        // Arrange
        var validator = new PasswordValidator();

        // Act
        var result = validator.IsCommonPassword(password);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsCommonPassword_NonCommonPassword_ReturnsFalse()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "Uncommon1!";

        // Act
        var result = validator.IsCommonPassword(password);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasUpperCase_ContainsUppercase_ReturnsTrue()
    {
        // Arrange
        var validator = new PasswordValidator();
        var input = "abcD";

        // Act
        var result = validator.HasUpperCase(input);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasLowerCase_ContainsLowercase_ReturnsTrue()
    {
        // Arrange
        var validator = new PasswordValidator();
        var input = "ABCd";

        // Act
        var result = validator.HasLowerCase(input);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasDigit_ContainsDigit_ReturnsTrue()
    {
        // Arrange
        var validator = new PasswordValidator();
        var input = "abc1";

        // Act
        var result = validator.HasDigit(input);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("abc!")]
    [InlineData("abc ")]
    [InlineData("abc.")]
    public void HasSpecialCharacter_ContainsSpecialCharacter_ReturnsTrue(string input)
    {
        // Arrange
        var validator = new PasswordValidator();

        // Act
        var result = validator.HasSpecialCharacter(input);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CountUniqueCharacters_RepeatedCharacters_ReturnsOne()
    {
        // Arrange
        var validator = new PasswordValidator();
        var input = "aaaaaa";

        // Act
        var result = validator.CountUniqueCharacters(input);

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public void CountUniqueCharacters_DistinctCharacters_ReturnsExpectedCount()
    {
        // Arrange
        var validator = new PasswordValidator();
        var input = "abcde123";

        // Act
        var result = validator.CountUniqueCharacters(input);

        // Assert
        Assert.Equal(8, result);
    }

    [Fact]
    public void GetStrengthLabel_StrengthBoundaryAtTwo_ReturnsWeak()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "aaaa";

        // Act
        var result = validator.GetStrengthLabel(password);

        // Assert
        Assert.Equal("Weak", result);
    }

    [Fact]
    public void GetStrengthLabel_StrengthBoundaryAtThree_ReturnsFair()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "abc1";

        // Act
        var result = validator.GetStrengthLabel(password);

        // Assert
        Assert.Equal("Fair", result);
    }

    [Fact]
    public void GetStrengthLabel_StrengthBoundaryAtSix_ReturnsStrong()
    {
        // Arrange
        var validator = new PasswordValidator();
        var password = "Abcdef1!";

        // Act
        var result = validator.GetStrengthLabel(password);

        // Assert
        Assert.Equal("Strong", result);
    }
}