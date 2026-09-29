namespace RomaERP.Application.Common.Interfaces;

public record FaceVerificationResult(bool Success, bool IsMatch, decimal? SimilarityPercent, string? FailureReason);

/// <summary>Compares a freshly-captured selfie against an employee's stored reference photo for
/// attendance check-in/out. The registered implementation (AwsRekognitionFaceVerificationProvider) stays
/// inert — <see cref="IsConfigured"/> false — until AWS Rekognition credentials are set in configuration;
/// until then GPS geofencing alone still confirms attendance, matching how this codebase's other
/// pluggable providers (payment gateway, bank feed, exchange rate) work before their own account
/// exists.</summary>
public interface IFaceVerificationProvider
{
    string Name { get; }
    bool IsConfigured { get; }
    Task<FaceVerificationResult> CompareFacesAsync(byte[] referenceImageBytes, byte[] candidateImageBytes, CancellationToken ct = default);
}
