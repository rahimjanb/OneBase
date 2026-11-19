import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_client.dart';
import '../../../core/api/api_providers.dart';
import '../../../core/cache/paged.dart';
import 'crm_models.dart';

/// Фильтры списка точек.
class CustomerQuery {
  const CustomerQuery({this.search = '', this.attention, this.status, this.sort});

  final String search;

  /// decline | lost | notvisited | new
  final String? attention;

  /// Active | Problem | Inactive
  final String? status;

  /// sales90 | trend | lastvisit | name (по умолчанию — продажи за 30 дней)
  final String? sort;

  CustomerQuery copyWith({String? search, String? Function()? attention, String? Function()? status, String? Function()? sort}) => CustomerQuery(
        search: search ?? this.search,
        attention: attention == null ? this.attention : attention(),
        status: status == null ? this.status : status(),
        sort: sort == null ? this.sort : sort(),
      );

  @override
  bool operator ==(Object other) =>
      other is CustomerQuery && other.search == search && other.attention == attention && other.status == status && other.sort == sort;

  @override
  int get hashCode => Object.hash(search, attention, status, sort);
}

class CrmApi {
  CrmApi(this._api);

  final ApiClient _api;

  static Map<String, Object?> _map(Object? j) => j! as Map<String, Object?>;

  Future<PageChunk<FieldCustomerRow>> customers(CustomerQuery q, int page, CancelToken cancel) => _api.get(
        '/api/field/customers',
        (j) {
          final m = _map(j);
          final items = [for (final e in m['items']! as List<Object?>) FieldCustomerRow.fromJson(_map(e))];
          final total = m['total'] as int? ?? 0;
          final size = m['pageSize'] as int? ?? 50;
          return PageChunk(items, hasMore: page * size < total);
        },
        query: {
          'search': q.search.trim().isEmpty ? null : q.search.trim(),
          'attention': q.attention,
          'status': q.status,
          'sort': q.sort,
          'page': page,
          'pageSize': 50,
        },
        cancel: cancel,
      );

  Future<FieldCustomerCard> customer(int id, CancelToken cancel) =>
      _api.get('/api/field/customers/$id', (j) => FieldCustomerCard.fromJson(_map(j)), cancel: cancel);

  Future<List<AuditEntry>> audit(int id, CancelToken cancel) => _api.get(
        '/api/field/audit',
        (j) => [for (final e in j! as List<Object?>) AuditEntry.fromJson(_map(e))],
        query: {'entityType': 'field_customer', 'entityId': '$id', 'take': 50},
        cancel: cancel,
      );

  /// Правка надстройки Sales Base: статус, приоритет, план, контакт, телефон, заметка. Linko не меняется.
  Future<void> update(int id, Map<String, Object?> body) => _api.put<void>('/api/field/customers/$id', body: body);
}

final crmApiProvider = Provider<CrmApi>((ref) => CrmApi(ref.watch(apiClientProvider)));
