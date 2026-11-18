import 'package:flutter/material.dart';

/// Значок OneBase («1Base») — тот же, что у сайта и PWA.
class BrandMark extends StatelessWidget {
  const BrandMark({super.key, this.size = 72});

  final double size;

  @override
  Widget build(BuildContext context) => Semantics(
        label: 'OneBase',
        image: true,
        child: ClipRRect(
          borderRadius: BorderRadius.circular(size * 0.23),
          child: Image.asset('assets/brand/1base-icon.png', width: size, height: size, filterQuality: FilterQuality.medium),
        ),
      );
}
