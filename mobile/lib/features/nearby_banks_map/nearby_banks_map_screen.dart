import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:geolocator/geolocator.dart';
import 'package:latlong2/latlong.dart' show LatLng;

import '../../theme/tokens.dart';
import 'organization.dart';
import 'organizations_repository.dart';

class NearbyBanksMapScreen extends ConsumerStatefulWidget {
  const NearbyBanksMapScreen({super.key});

  @override
  ConsumerState<NearbyBanksMapScreen> createState() =>
      _NearbyBanksMapScreenState();
}

class _NearbyBanksMapScreenState
    extends ConsumerState<NearbyBanksMapScreen> {
  final MapController _mapController = MapController();
  final TextEditingController _searchController =
      TextEditingController();

  Position? _currentPosition;
  String _searchQuery = '';
  String? _lastFitSignature;

  @override
  void initState() {
    super.initState();

    _searchController.addListener(() {
      if (!mounted) return;

      setState(() {
        _searchQuery =
            _searchController.text.trim().toLowerCase();
      });
    });

    WidgetsBinding.instance.addPostFrameCallback((_) {
      _loadCurrentLocation();
    });
  }

  @override
  void dispose() {
    _searchController.dispose();
    _mapController.dispose();
    super.dispose();
  }

  Future<void> _loadCurrentLocation({
    bool moveMap = true,
  }) async {
    try {
      final serviceEnabled =
          await Geolocator.isLocationServiceEnabled();

      if (!serviceEnabled) {
        if (!mounted) return;

        await _showLocationMessage(
          title: 'Location is turned off',
          message:
              'Turn on location services to center the map on your current position.',
          actionLabel: 'Open settings',
          onAction: () async {
            await Geolocator.openLocationSettings();
          },
        );

        return;
      }

      var permission =
          await Geolocator.checkPermission();

      if (permission == LocationPermission.denied) {
        permission =
            await Geolocator.requestPermission();
      }

      if (permission == LocationPermission.denied) {
        if (!mounted) return;

        await _showLocationMessage(
          title: 'Location permission needed',
          message:
              'Allow location access to use the current-location feature.',
        );

        return;
      }

      if (permission ==
          LocationPermission.deniedForever) {
        if (!mounted) return;

        await _showLocationMessage(
          title: 'Location permission blocked',
          message:
              'Location permission is permanently denied. Open app settings and allow location access.',
          actionLabel: 'Open settings',
          onAction: () async {
            await Geolocator.openAppSettings();
          },
        );

        return;
      }

      // Without a time limit, this hangs indefinitely on a device/emulator
      // with no GPS fix available (common on Android emulators unless a
      // mock location is set) — reproduced live: the screen got stuck on
      // "Loading blood banks..." until Android killed it with an ANR. A
      // timeout lets the existing catch block below show its intended
      // graceful fallback instead.
      final position =
          await Geolocator.getCurrentPosition(
        locationSettings: const LocationSettings(
          accuracy: LocationAccuracy.high,
          distanceFilter: 0,
        ),
        timeLimit: const Duration(seconds: 10),
      );

      if (!mounted) return;

      setState(() {
        _currentPosition = position;
      });

      if (moveMap) {
        _mapController.move(
          LatLng(
            position.latitude,
            position.longitude,
          ),
          13.5,
        );
      }
    } catch (_) {
      if (!mounted) return;

      await _showLocationMessage(
        title: 'Could not get your location',
        message:
            'You can still browse the available organizations on the map.',
      );
    }
  }

  Future<void> _showLocationMessage({
    required String title,
    required String message,
    String? actionLabel,
    Future<void> Function()? onAction,
  }) async {
    if (!mounted) return;

    await showDialog<void>(
      context: context,
      builder: (context) {
        return AlertDialog(
          title: Text(title),
          content: Text(message),
          actions: [
            TextButton(
              onPressed: () {
                Navigator.of(context).pop();
              },
              child: const Text('OK'),
            ),
            if (actionLabel != null &&
                onAction != null)
              FilledButton(
                onPressed: () async {
                  Navigator.of(context).pop();
                  await onAction();
                },
                child: Text(actionLabel),
              ),
          ],
        );
      },
    );
  }

  Future<void> _refreshOrganizations() async {
    ref.invalidate(organizationsProvider);

    try {
      await ref.read(organizationsProvider.future);
    } catch (_) {
      // The provider itself displays the error state.
    }
  }

  List<Organization> _filteredOrganizations(
    List<Organization> organizations,
  ) {
    final visible =
        organizations.where((organization) {
      if (_searchQuery.isEmpty) {
        return true;
      }

      final query = _searchQuery;

      return organization.name
              .toLowerCase()
              .contains(query) ||
          organization.address
              .toLowerCase()
              .contains(query) ||
          organization.displayType
              .toLowerCase()
              .contains(query);
    }).toList();

    if (_currentPosition == null) {
      return visible;
    }

    visible.sort((a, b) {
      final distanceA =
          Geolocator.distanceBetween(
        _currentPosition!.latitude,
        _currentPosition!.longitude,
        a.latitude,
        a.longitude,
      );

      final distanceB =
          Geolocator.distanceBetween(
        _currentPosition!.latitude,
        _currentPosition!.longitude,
        b.latitude,
        b.longitude,
      );

      return distanceA.compareTo(distanceB);
    });

    return visible;
  }

  List<Organization> _mapOrganizations(
    List<Organization> organizations,
  ) {
    return organizations
        .where(_isProjectCoordinate)
        .toList();
  }

  bool _isProjectCoordinate(
    Organization organization,
  ) {
    if (!organization.hasValidCoordinates) {
      return false;
    }

    // Project organizations are expected to be in Sri Lanka.
    // This prevents one malformed coordinate from zooming the
    // entire map away from the actual organizations.
    return organization.latitude >= 5.5 &&
        organization.latitude <= 10.2 &&
        organization.longitude >= 79.0 &&
        organization.longitude <= 82.2;
  }

  void _fitToOrganizations(
    List<Organization> organizations,
  ) {
    final mapOrganizations =
        _mapOrganizations(organizations);

    if (mapOrganizations.isEmpty) {
      return;
    }

    final signature = mapOrganizations
        .map((organization) => organization.id)
        .join('|');

    if (_lastFitSignature == signature) {
      return;
    }

    _lastFitSignature = signature;

    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;

      final coordinates = mapOrganizations
          .map(
            (organization) => LatLng(
              organization.latitude,
              organization.longitude,
            ),
          )
          .toList();

      if (coordinates.length == 1) {
        _mapController.move(
          coordinates.first,
          13.0,
        );

        return;
      }

      _mapController.fitCamera(
        CameraFit.coordinates(
          coordinates: coordinates,
          padding: const EdgeInsets.fromLTRB(
            60,
            180,
            60,
            110,
          ),
          maxZoom: 14,
          minZoom: 6,
        ),
      );
    });
  }

  double? _distanceInKm(
    Organization organization,
  ) {
    if (_currentPosition == null) {
      return null;
    }

    final meters =
        Geolocator.distanceBetween(
      _currentPosition!.latitude,
      _currentPosition!.longitude,
      organization.latitude,
      organization.longitude,
    );

    return meters / 1000;
  }

  String _formatDistance(
    double distanceKm,
  ) {
    if (distanceKm < 1) {
      final meters =
          (distanceKm * 1000).round();

      return '$meters m away';
    }

    return '${distanceKm.toStringAsFixed(1)} km away';
  }

  void _showOrganizationDetails(
    Organization organization,
  ) {
    final distance =
        _distanceInKm(organization);

    showModalBottomSheet<void>(
      context: context,
      backgroundColor: AppColors.surface,
      isScrollControlled: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(
          top: Radius.circular(AppRadii.md),
        ),
      ),
      builder: (context) {
        return SafeArea(
          child: Padding(
            padding: const EdgeInsets.fromLTRB(
              AppSpacing.md,
              AppSpacing.sm,
              AppSpacing.md,
              AppSpacing.md,
            ),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment:
                  CrossAxisAlignment.start,
              children: [
                Center(
                  child: Container(
                    width: 40,
                    height: 4,
                    decoration: BoxDecoration(
                      color:
                          AppColors.hairlineStrong,
                      borderRadius:
                          BorderRadius.circular(
                        AppRadii.pill,
                      ),
                    ),
                  ),
                ),
                const SizedBox(
                  height: AppSpacing.lg,
                ),
                Row(
                  children: [
                    Container(
                      width: 44,
                      height: 44,
                      alignment: Alignment.center,
                      decoration: BoxDecoration(
                        color:
                            organization.isBloodBank
                                ? AppColors.primarySubtle
                                : AppColors.agentSubtle,
                        borderRadius:
                            BorderRadius.circular(
                          AppRadii.sm,
                        ),
                      ),
                      child: Icon(
                        organization.isBloodBank
                            ? Icons.bloodtype_rounded
                            : Icons
                                .local_hospital_outlined,
                        color:
                            organization.isBloodBank
                                ? AppColors.primary
                                : AppColors.agent,
                      ),
                    ),
                    const SizedBox(
                      width: AppSpacing.sm,
                    ),
                    Expanded(
                      child: Column(
                        crossAxisAlignment:
                            CrossAxisAlignment.start,
                        children: [
                          Text(
                            organization.name,
                            style: const TextStyle(
                              fontSize: 18,
                              fontWeight:
                                  FontWeight.w700,
                              color: AppColors.ink,
                            ),
                          ),
                          const SizedBox(height: 2),
                          Text(
                            organization.displayType,
                            style: const TextStyle(
                              fontSize: 13,
                              color:
                                  AppColors.inkMuted,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
                const SizedBox(
                  height: AppSpacing.lg,
                ),
                _DetailRow(
                  icon:
                      Icons.location_on_outlined,
                  text:
                      organization.address.isEmpty
                          ? 'Address unavailable'
                          : organization.address,
                ),
                const SizedBox(
                  height: AppSpacing.sm,
                ),
                _DetailRow(
                  icon: Icons.phone_outlined,
                  text:
                      organization.phoneNumber.isEmpty
                          ? 'Phone unavailable'
                          : organization.phoneNumber,
                ),
                if (distance != null) ...[
                  const SizedBox(
                    height: AppSpacing.sm,
                  ),
                  _DetailRow(
                    icon:
                        Icons.near_me_outlined,
                    text: _formatDistance(
                      distance,
                    ),
                  ),
                ],
                const SizedBox(
                  height: AppSpacing.lg,
                ),
              ],
            ),
          ),
        );
      },
    );
  }

  List<Marker> _buildOrganizationMarkers(
    List<Organization> organizations,
  ) {
    return organizations
        .where(_isProjectCoordinate)
        .map(
          (organization) => Marker(
            point: LatLng(
              organization.latitude,
              organization.longitude,
            ),
            width: 52,
            height: 60,
            alignment: Alignment.bottomCenter,
            child: GestureDetector(
              onTap: () =>
                  _showOrganizationDetails(
                organization,
              ),
              child: Column(
                mainAxisSize:
                    MainAxisSize.min,
                children: [
                  Container(
                    width: 42,
                    height: 42,
                    alignment: Alignment.center,
                    decoration: BoxDecoration(
                      color:
                          organization.isBloodBank
                              ? AppColors.primary
                              : AppColors.agent,
                      shape: BoxShape.circle,
                      boxShadow:
                          AppElevation.lifted,
                    ),
                    child: Icon(
                      organization.isBloodBank
                          ? Icons.bloodtype_rounded
                          : Icons
                              .local_hospital_rounded,
                      color:
                          AppColors.onPrimary,
                      size: 21,
                    ),
                  ),
                  CustomPaint(
                    size:
                        const Size(12, 7),
                    painter:
                        _MarkerPointerPainter(
                      color:
                          organization.isBloodBank
                              ? AppColors.primary
                              : AppColors.agent,
                    ),
                  ),
                ],
              ),
            ),
          ),
        )
        .toList();
  }

  @override
  Widget build(
    BuildContext context,
  ) {
    final organizationsAsync =
        ref.watch(organizationsProvider);

    return Scaffold(
      backgroundColor: AppColors.canvas,
      appBar: AppBar(
        backgroundColor:
            AppColors.canvas,
        surfaceTintColor:
            Colors.transparent,
        elevation: 0,
        title: const Text(
          'Nearby Blood Banks',
          style: TextStyle(
            color: AppColors.ink,
            fontSize: 20,
            fontWeight:
                FontWeight.w700,
          ),
        ),
        iconTheme:
            const IconThemeData(
          color: AppColors.ink,
        ),
        actions: [
          IconButton(
            tooltip: 'Refresh',
            onPressed:
                _refreshOrganizations,
            icon: const Icon(
              Icons.refresh_rounded,
            ),
          ),
        ],
      ),
      body: organizationsAsync.when(
        loading: () =>
            const _LoadingMapState(),
        error: (error, _) =>
            _ErrorState(
          message: error.toString(),
          onRetry:
              _refreshOrganizations,
        ),
        data: (organizations) {
          _fitToOrganizations(
            organizations,
          );

          final visibleOrganizations =
              _filteredOrganizations(
            organizations,
          );

          final mapOrganizations =
              _mapOrganizations(
            visibleOrganizations,
          );

          final markers =
              _buildOrganizationMarkers(
            mapOrganizations,
          );

          final currentPosition =
              _currentPosition;

          final bloodBankCount =
              mapOrganizations
                  .where(
                    (organization) =>
                        organization
                            .isBloodBank,
                  )
                  .length;

          final hospitalCount =
              mapOrganizations
                  .where(
                    (organization) =>
                        organization
                            .isHospital,
                  )
                  .length;

          return Stack(
            children: [
              Positioned.fill(
                child: FlutterMap(
                  mapController:
                      _mapController,
                  options: const MapOptions(
                    initialCenter: LatLng(
                      7.8731,
                      80.7718,
                    ),
                    initialZoom: 7.2,
                    maxZoom: 18,
                    minZoom: 5,
                  ),
                  children: [
                    TileLayer(
                      urlTemplate:
                          'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
                      userAgentPackageName:
                          'blood_donation_network',
                    ),
                    if (currentPosition !=
                        null)
                      MarkerLayer(
                        markers: [
                          Marker(
                            point: LatLng(
                              currentPosition
                                  .latitude,
                              currentPosition
                                  .longitude,
                            ),
                            width: 30,
                            height: 30,
                            child: Container(
                              decoration:
                                  BoxDecoration(
                                color:
                                    AppColors.agent,
                                shape:
                                    BoxShape.circle,
                                border:
                                    Border.all(
                                  color:
                                      AppColors.surface,
                                  width: 4,
                                ),
                                boxShadow:
                                    AppElevation
                                        .lifted,
                              ),
                            ),
                          ),
                        ],
                      ),
                    MarkerLayer(
                      markers: markers,
                    ),
                    RichAttributionWidget(
                      attributions: [
                        TextSourceAttribution(
                          'OpenStreetMap contributors',
                          onTap: () {},
                        ),
                      ],
                    ),
                  ],
                ),
              ),

              Positioned(
                top: AppSpacing.md,
                left: AppSpacing.gutter,
                right: AppSpacing.gutter,
                child: Material(
                  color: AppColors.surface,
                  borderRadius:
                      BorderRadius.circular(
                    AppRadii.sm,
                  ),
                  elevation: 0,
                  child: TextField(
                    controller:
                        _searchController,
                    textInputAction:
                        TextInputAction.search,
                    decoration:
                        InputDecoration(
                      hintText:
                          'Search blood banks or hospitals',
                      hintStyle:
                          const TextStyle(
                        color:
                            AppColors.inkFaint,
                      ),
                      prefixIcon:
                          const Icon(
                        Icons.search_rounded,
                        color:
                            AppColors.inkMuted,
                      ),
                      suffixIcon:
                          _searchQuery.isEmpty
                              ? null
                              : IconButton(
                                  onPressed:
                                      _searchController
                                          .clear,
                                  icon:
                                      const Icon(
                                    Icons
                                        .clear_rounded,
                                    color:
                                        AppColors
                                            .inkMuted,
                                  ),
                                ),
                      filled: true,
                      fillColor:
                          AppColors.surface,
                      border:
                          OutlineInputBorder(
                        borderRadius:
                            BorderRadius.circular(
                          AppRadii.sm,
                        ),
                        borderSide:
                            const BorderSide(
                          color:
                              AppColors.hairline,
                        ),
                      ),
                      enabledBorder:
                          OutlineInputBorder(
                        borderRadius:
                            BorderRadius.circular(
                          AppRadii.sm,
                        ),
                        borderSide:
                            const BorderSide(
                          color:
                              AppColors.hairline,
                        ),
                      ),
                      focusedBorder:
                          OutlineInputBorder(
                        borderRadius:
                            BorderRadius.circular(
                          AppRadii.sm,
                        ),
                        borderSide:
                            const BorderSide(
                          color:
                              AppColors.primary,
                          width: 1.2,
                        ),
                      ),
                    ),
                  ),
                ),
              ),

              Positioned(
                left: AppSpacing.gutter,
                right: AppSpacing.gutter,
                bottom: AppSpacing.md,
                child: Row(
                  children: [
                    _MapLegend(
                      bloodBanks:
                          bloodBankCount,
                      hospitals:
                          hospitalCount,
                    ),
                    const Spacer(),
                    FloatingActionButton.small(
                      heroTag:
                          'nearby-banks-current-location',
                      backgroundColor:
                          AppColors.surface,
                      foregroundColor:
                          AppColors.primary,
                      elevation: 3,
                      onPressed: () =>
                          _loadCurrentLocation(
                        moveMap: true,
                      ),
                      child: const Icon(
                        Icons
                            .my_location_rounded,
                      ),
                    ),
                  ],
                ),
              ),

              if (visibleOrganizations
                  .isEmpty)
                Positioned(
                  left:
                      AppSpacing.gutter,
                  right:
                      AppSpacing.gutter,
                  bottom:
                      AppSpacing.xxl,
                  child:
                      _EmptyResultsCard(
                    hasSearch:
                        _searchQuery
                            .isNotEmpty,
                  ),
                ),
            ],
          );
        },
      ),
    );
  }
}

class _DetailRow
    extends StatelessWidget {
  const _DetailRow({
    required this.icon,
    required this.text,
  });

  final IconData icon;
  final String text;

  @override
  Widget build(
    BuildContext context,
  ) {
    return Row(
      crossAxisAlignment:
          CrossAxisAlignment.start,
      children: [
        Icon(
          icon,
          size: 19,
          color:
              AppColors.inkMuted,
        ),
        const SizedBox(
          width: AppSpacing.sm,
        ),
        Expanded(
          child: Text(
            text,
            style:
                const TextStyle(
              fontSize: 14,
              height: 1.4,
              color:
                  AppColors.inkSecondary,
            ),
          ),
        ),
      ],
    );
  }
}

class _MapLegend
    extends StatelessWidget {
  const _MapLegend({
    required this.bloodBanks,
    required this.hospitals,
  });

  final int bloodBanks;
  final int hospitals;

  @override
  Widget build(
    BuildContext context,
  ) {
    return Container(
      padding:
          const EdgeInsets.symmetric(
        horizontal:
            AppSpacing.sm,
        vertical:
            AppSpacing.xs,
      ),
      decoration:
          BoxDecoration(
        color:
            AppColors.surface,
        borderRadius:
            BorderRadius.circular(
          AppRadii.sm,
        ),
        boxShadow:
            AppElevation.lifted,
      ),
      child: Row(
        mainAxisSize:
            MainAxisSize.min,
        children: [
          const Icon(
            Icons
                .bloodtype_rounded,
            size: 16,
            color:
                AppColors.primary,
          ),
          const SizedBox(
            width: 4,
          ),
          Text(
            '$bloodBanks',
            style:
                const TextStyle(
              fontWeight:
                  FontWeight.w700,
              color:
                  AppColors.ink,
            ),
          ),
          const SizedBox(
            width: AppSpacing.sm,
          ),
          const Icon(
            Icons
                .local_hospital_rounded,
            size: 16,
            color:
                AppColors.agent,
          ),
          const SizedBox(
            width: 4,
          ),
          Text(
            '$hospitals',
            style:
                const TextStyle(
              fontWeight:
                  FontWeight.w700,
              color:
                  AppColors.ink,
            ),
          ),
        ],
      ),
    );
  }
}

class _EmptyResultsCard
    extends StatelessWidget {
  const _EmptyResultsCard({
    required this.hasSearch,
  });

  final bool hasSearch;

  @override
  Widget build(
    BuildContext context,
  ) {
    return Container(
      padding:
          const EdgeInsets.all(
        AppSpacing.md,
      ),
      decoration:
          BoxDecoration(
        color:
            AppColors.surface,
        borderRadius:
            BorderRadius.circular(
          AppRadii.md,
        ),
        boxShadow:
            AppElevation.lifted,
      ),
      child: Row(
        children: [
          const Icon(
            Icons.search_off_rounded,
            color:
                AppColors.inkMuted,
          ),
          const SizedBox(
            width: AppSpacing.sm,
          ),
          Expanded(
            child: Text(
              hasSearch
                  ? 'No organizations match your search.'
                  : 'No organizations are available right now.',
              style:
                  const TextStyle(
                color:
                    AppColors
                        .inkSecondary,
                fontSize: 14,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _LoadingMapState
    extends StatelessWidget {
  const _LoadingMapState();

  @override
  Widget build(
    BuildContext context,
  ) {
    return const Center(
      child: Padding(
        padding:
            EdgeInsets.all(
          AppSpacing.xl,
        ),
        child: Column(
          mainAxisSize:
              MainAxisSize.min,
          children: [
            CircularProgressIndicator(
              color:
                  AppColors.primary,
            ),
            SizedBox(
              height:
                  AppSpacing.md,
            ),
            Text(
              'Loading blood banks...',
              style: TextStyle(
                color:
                    AppColors.inkMuted,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ErrorState
    extends StatelessWidget {
  const _ErrorState({
    required this.message,
    required this.onRetry,
  });

  final String message;
  final Future<void>
      Function() onRetry;

  @override
  Widget build(
    BuildContext context,
  ) {
    return Center(
      child: Padding(
        padding:
            const EdgeInsets.all(
          AppSpacing.xl,
        ),
        child: Column(
          mainAxisSize:
              MainAxisSize.min,
          children: [
            const Icon(
              Icons.cloud_off_rounded,
              size: 42,
              color:
                  AppColors.inkMuted,
            ),
            const SizedBox(
              height:
                  AppSpacing.md,
            ),
            const Text(
              'Could not load blood banks',
              textAlign:
                  TextAlign.center,
              style:
                  TextStyle(
                fontSize: 18,
                fontWeight:
                    FontWeight.w700,
                color:
                    AppColors.ink,
              ),
            ),
            const SizedBox(
              height:
                  AppSpacing.xs,
            ),
            Text(
              message,
              maxLines: 3,
              overflow:
                  TextOverflow.ellipsis,
              textAlign:
                  TextAlign.center,
              style:
                  const TextStyle(
                color:
                    AppColors.inkMuted,
                fontSize: 13,
              ),
            ),
            const SizedBox(
              height:
                  AppSpacing.md,
            ),
            FilledButton(
              onPressed: onRetry,
              style:
                  FilledButton
                      .styleFrom(
                backgroundColor:
                    AppColors.primary,
                foregroundColor:
                    AppColors.onPrimary,
                shape:
                    RoundedRectangleBorder(
                  borderRadius:
                      BorderRadius
                          .circular(
                    AppRadii.sm,
                  ),
                ),
              ),
              child:
                  const Text(
                'Try again',
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _MarkerPointerPainter
    extends CustomPainter {
  const _MarkerPointerPainter({
    required this.color,
  });

  final Color color;

  @override
  void paint(
    Canvas canvas,
    Size size,
  ) {
    final path = Path()
      ..moveTo(0, 0)
      ..lineTo(
        size.width / 2,
        size.height,
      )
      ..lineTo(
        size.width,
        0,
      )
      ..close();

    canvas.drawPath(
      path,
      Paint()..color = color,
    );
  }

  @override
  bool shouldRepaint(
    covariant
        _MarkerPointerPainter oldDelegate,
  ) {
    return oldDelegate.color !=
        color;
  }
}