param(
    [xml] $Document
);

foreach( $file in $Document.unattend.Extensions.File ) {
    $path = [System.Environment]::ExpandEnvironmentVariables(
        $file.GetAttribute( 'path' )
    );
    mkdir -Path ( [System.IO.Path]::GetDirectoryName( $path ) ) -ErrorAction 'SilentlyContinue';
    if( $file.GetAttribute( 'encoding' ) -eq 'base64' ) {
        [System.IO.File]::WriteAllBytes( $path, [System.Convert]::FromBase64String( $file.InnerText.Trim() ) );
        continue;
    }
    $encoding = switch( [System.IO.Path]::GetExtension( $path ) ) {
        { $_ -in '.ps1', '.xml' } { [System.Text.Encoding]::UTF8; }
        { $_ -in '.reg', '.vbs', '.js' } { [System.Text.UnicodeEncoding]::new( $false, $true ); }
        default { [System.Text.Encoding]::Default; }
    };
    [System.IO.File]::WriteAllBytes( $path, ( $encoding.GetPreamble() + $encoding.GetBytes( $file.InnerText.Trim() ) ) );
}
