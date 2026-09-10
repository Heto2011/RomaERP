namespace RomaERP.Application.HR.DTOs;

public class WorkLocationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public int GeofenceRadiusMeters { get; set; }
    public bool IsActive { get; set; }
}

public class SaveWorkLocationDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public int GeofenceRadiusMeters { get; set; } = 20;
    public bool IsActive { get; set; } = true;
}
