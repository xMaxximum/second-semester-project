window.blazorGeolocation = {
    getCurrentPosition: function () {
        return new Promise((resolve, reject) => {
            if (!navigator.geolocation) {
                reject('Geolocation is not supported by this browser.');
                return;
            }

            const options = {
                enableHighAccuracy: true,
                timeout: 15000,
                maximumAge: 60000 // 1 minute
            };

            navigator.geolocation.getCurrentPosition(
                (position) => {
                    const lat = position.coords.latitude;
                    const lng = position.coords.longitude;
                    const accuracy = position.coords.accuracy;

                    const result = {
                        latitude: lat,
                        longitude: lng,
                        accuracy: accuracy
                    };

                    resolve(JSON.stringify(result));
                },
                (error) => {
                    console.error('Geolocation error:', error);
                    let errorMessage = '';
                    switch (error.code) {
                        case 1: // PERMISSION_DENIED
                            errorMessage = 'Location access denied by user. Please enable location permissions and try again.';
                            break;
                        case 2: // POSITION_UNAVAILABLE
                            errorMessage = 'Location information is unavailable. Please check your internet connection.';
                            break;
                        case 3: // TIMEOUT
                            errorMessage = 'Location request timed out. Please try again.';
                            break;
                        default:
                            errorMessage = 'An unknown error occurred while getting location.';
                            break;
                    }
                    reject(errorMessage);
                },
                options
            );
        });
    }
};
